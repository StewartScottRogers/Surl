using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ldap.LdapDirectoryFixture;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Writes the directory back as LDIF for a test (ADR-0164 decision 9, BL-1219): <c>userPassword</c> omitted, values
/// safe or base64, long lines folded, and <see cref="LdifReader"/> reads the text back as the same entries.
/// </summary>
[TestClass]
public sealed class LdifWriterTests
{
    [TestMethod]
    public void Write_NoEntries_IsTheVersionLineAlone() =>
        Assert.AreEqual("version: 1\n", LdifWriter.Write([]));

    [TestMethod]
    public void Write_Entries_AreRecordsInOrderEachAfterABlankLine()
    {
        var text = LdifWriter.Write(
        [
            Entry("dc=example,dc=com", ("objectClass", "domain"), ("dc", "example")),
            Entry("cn=a,dc=example,dc=com", ("cn", "a"), ("cn", "b"), ("cn;lang-fr", "c")),
        ]);

        Assert.AreEqual(
            "version: 1\n\ndn: dc=example,dc=com\nobjectClass: domain\ndc: example\n\ndn: cn=a,dc=example,dc=com\ncn: a\ncn: b\ncn;lang-fr: c\n",
            text);
    }

    [TestMethod]
    [DataRow("userPassword")]
    [DataRow("USERPASSWORD")]
    [DataRow("userpassword;x-hash")]
    [DataRow("userPassword;binary")]
    public void Write_AUserPasswordWhateverItsCaseOrOptions_IsOmitted(string description)
    {
        var text = LdifWriter.Write([Entry("cn=a,dc=com", ("cn", "a"), (description, "secret"), ("sn", "b"))]);

        Assert.AreEqual("version: 1\n\ndn: cn=a,dc=com\ncn: a\nsn: b\n", text);
        Assert.DoesNotContain("secret", text);
    }

    [TestMethod]
    [DataRow("plain", "cn: plain")]
    [DataRow("a b", "cn: a b")]
    [DataRow("", "cn: ")]
    [DataRow(" lead", "cn:: IGxlYWQ=")]
    [DataRow(":colon", "cn:: OmNvbG9u")]
    [DataRow("<url", "cn:: PHVybA==")]
    [DataRow("trail ", "cn:: dHJhaWwg")]
    [DataRow("caf\u00e9", "cn:: Y2Fmw6k=")]
    [DataRow("a\nb", "cn:: YQpi")]
    [DataRow("a\rb", "cn:: YQ1i")]
    [DataRow("a\0b", "cn:: YQBi")]
    public void Write_AValue_IsASafeStringOrBase64(string value, string expectedLine)
    {
        var text = LdifWriter.Write([Entry("cn=a,dc=com", ("cn", value))]);

        Assert.AreEqual("version: 1\n\ndn: cn=a,dc=com\n" + expectedLine + "\n", text);
    }

    [TestMethod]
    public void Write_ADnThatIsNotASafeString_IsBase64()
    {
        var text = LdifWriter.Write([Entry("cn=caf\u00e9,dc=com", ("cn", "x"))]);

        StringAssert.Contains(text, "dn:: " + Convert.ToBase64String(Encoding.UTF8.GetBytes("cn=caf\u00e9,dc=com")) + "\n");
    }

    [TestMethod]
    public void Write_ALongLine_IsFoldedAt76ColumnsAndReadsBackWhole()
    {
        var value = new string('a', 200);
        var entries = new[] { Entry("cn=a,dc=com", ("description", value)) };

        var text = LdifWriter.Write(entries);

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(2).ToArray();
        Assert.IsTrue(lines.All(line => line.Length <= 76), text);
        Assert.IsTrue(lines.Skip(1).All(line => line[0] == ' '), text);
        Assert.HasCount(3, lines);
        CollectionAssert.AreEqual(
            Encoding.UTF8.GetBytes(value),
            LdifReader.Read(Encoding.UTF8.GetBytes(text)).Single().Entry.Attributes.Single().Values.Single());
    }

    [TestMethod]
    public void Write_AFoldedLineExactlyAtTheLimit_IsNotFolded()
    {
        var value = new string('a', 76 - "cn: ".Length);

        var text = LdifWriter.Write([Entry("cn=a,dc=com", ("cn", value))]);

        Assert.AreEqual("version: 1\n\ndn: cn=a,dc=com\ncn: " + value + "\n", text);
    }

    [TestMethod]
    public void Write_ThenLdifReaderRead_ReproducesTheEntriesLessUserPassword()
    {
        var entries = new List<LdapEntry>(PeopleEntries())
        {
            Entry(
                "cn=odd,dc=example,dc=com",
                ("cn", "odd"),
                ("description", " leading and trailing "),
                ("note", "line\r\nbreak"),
                ("empty", ""),
                ("long", new string('z', 300) + "\u00e9"),
                ("userPassword", "hunter2"),
                ("userPassword;x", "hunter3")),
        };

        var records = LdifReader.Read(Encoding.UTF8.GetBytes(LdifWriter.Write(entries)));

        Assert.HasCount(entries.Count, records);
        for (var index = 0; index < entries.Count; index++)
        {
            var expected = entries[index];
            var actual = records[index].Entry;
            Assert.AreEqual(expected.Dn.Text, actual.Dn.Text);
            var kept = expected.Attributes.Where(a => !a.Description.Type.Equals("userPassword", StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.HasCount(kept.Length, actual.Attributes);
            for (var attribute = 0; attribute < kept.Length; attribute++)
            {
                Assert.AreEqual(kept[attribute].Description.Text, actual.Attributes[attribute].Description.Text);
                Assert.HasCount(kept[attribute].Values.Count, actual.Attributes[attribute].Values);
                for (var value = 0; value < kept[attribute].Values.Count; value++)
                {
                    CollectionAssert.AreEqual(kept[attribute].Values[value], actual.Attributes[attribute].Values[value]);
                }
            }
        }
    }

    [TestMethod]
    public void ExportLdif_ConfiguredAndSeededEntriesAlike_PublishesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var directory = new LdapDirectory(
            [Entry("dc=example,dc=com", ("objectClass", "domain"), ("dc", "example"), ("userPassword", "secret"))],
            TimeProvider.System,
            events: events);
        directory.SeedEntries("dn: cn=alice,dc=example,dc=com\ncn: alice\n");
        var published = events.Events.Count;

        var text = directory.ExportLdif();

        Assert.AreEqual(
            "version: 1\n\ndn: dc=example,dc=com\nobjectClass: domain\ndc: example\n\ndn: cn=alice,dc=example,dc=com\ncn: alice\n",
            text);
        Assert.HasCount(published, events.Events);
    }

    [TestMethod]
    public void ExportDirectoryLdif_AServerWithSeededEntries_WritesThem()
    {
        var server = new LdapProtocolServer(
            new UnitTestAuthenticationPolicy(PasswordLoginVerdict.Accepted), new UnitTestSaslAuthenticationPolicy(), TimeProvider.System);
        Assert.AreEqual("version: 1\n", server.ExportDirectoryLdif());

        server.SeedDirectoryEntries("dn: dc=example,dc=com\ndc: example\nuserPassword: x\n");

        Assert.AreEqual("version: 1\n\ndn: dc=example,dc=com\ndc: example\n", server.ExportDirectoryLdif());
    }
}
