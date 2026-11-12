namespace Surl.Cli;

/// <summary>
/// <see cref="ConfiguredShareReader"/> reading <c>&lt;name&gt;=&lt;path&gt;[;readonly][;protocols=&lt;scheme&gt;,...]</c>
/// items (ADR-0101 decisions 3 to 5, ADR-0102 decision 2.1).
/// </summary>
[TestClass]
public sealed class ConfiguredShareReaderTests
{
    [TestMethod]
    public void Read_NameAndPath_IsWritableAndUnrestricted()
    {
        var share = Accepted("team=shares/team");

        Assert.AreEqual("team", share.Name);
        Assert.AreEqual("shares/team", share.Path);
        Assert.IsFalse(share.IsReadOnly);
        Assert.IsNull(share.Protocols);
    }

    [TestMethod]
    public void Read_PathHoldingTheSeparator_SplitsAtTheFirstEquals() =>
        Assert.AreEqual("a=b", Accepted("team=a=b").Path);

    [TestMethod]
    public void Read_WindowsPath_NeedsNoEscaping() =>
        Assert.AreEqual(@"C:\data\team", Accepted(@"team=C:\data\team").Path);

    [TestMethod]
    [DataRow("team=p;readonly", DisplayName = "Lower case")]
    [DataRow("team=p;READONLY", DisplayName = "Any case")]
    public void Read_ReadOnly_MakesTheShareReadOnly(string argument) =>
        Assert.IsTrue(Accepted(argument).IsReadOnly);

    [TestMethod]
    public void Read_Protocols_AreLowerCasedInOrderWithoutTheirTlsTwins() =>
        CollectionAssert.AreEqual(new[] { "gopher", "ftp" }, Accepted("team=p;protocols=Gopher,FTP").Protocols!.ToArray());

    [TestMethod]
    public void Read_ReadOnlyAndProtocols_AreBothRead()
    {
        var share = Accepted("team=p;protocols=sftp;readonly");

        Assert.IsTrue(share.IsReadOnly);
        CollectionAssert.AreEqual(new[] { "sftp" }, share.Protocols!.ToArray());
    }

    [TestMethod]
    public void Read_PercentEncodedFields_AreDecodedAfterSplitting()
    {
        var share = Accepted("a%3Db%25=p%3Bq");

        Assert.AreEqual("a=b%", share.Name);
        Assert.AreEqual("p;q", share.Path);
    }

    [TestMethod]
    [DataRow("team", "expected <name>=<path>[;readonly][;protocols=<scheme>,...]", DisplayName = "No separator")]
    [DataRow("=p", "the name is empty", DisplayName = "Empty name")]
    [DataRow("team=", "an empty field", DisplayName = "Empty path")]
    [DataRow("team=p;", "an empty field", DisplayName = "Trailing semicolon")]
    [DataRow("team=a%00b", "a control character", DisplayName = "U+0000 in the path")]
    [DataRow("te%09am=p", "a control character", DisplayName = "Control character in the name")]
    [DataRow("team=p;readonly=yes", "flag readonly takes no value", DisplayName = "Flag given a value")]
    [DataRow("team=p;readonly;readonly", "key readonly is given twice", DisplayName = "Flag twice")]
    [DataRow("team=p;read-only", "unknown flag read-only", DisplayName = "Misspelt flag")]
    [DataRow("team=p;quota=1", "unknown key quota", DisplayName = "Unknown key")]
    [DataRow("team=p;protocols", "key protocols needs a value", DisplayName = "Protocols without a value")]
    [DataRow("team=p;protocols=nosuch", "unknown scheme nosuch", DisplayName = "Unknown scheme")]
    [DataRow("team=p;protocols=ftp,FTP", "member ftp is given twice", DisplayName = "Scheme twice")]
    [DataRow("team=p%zz", "a malformed percent escape", DisplayName = "Bad escape")]
    public void Read_MalformedItem_IsRefusedWithTheAdrsText(string argument, string expected)
    {
        var failure = ConfiguredShareReader.Read(argument, out var share);

        Assert.AreEqual(expected, failure);
        Assert.AreSame(ConfiguredShareReader.Empty, share);
    }

    [TestMethod]
    public void Reading_ArgumentType_IsNamedByTheShape() =>
        Assert.AreEqual(ConfiguredShareReader.Shape.Reading.Type, ConfiguredShareReader.Reading.Type);

    [TestMethod]
    public void ShareGivenTwice_NamesTheShare() =>
        Assert.AreEqual("share team is given twice", ConfiguredShareReader.ShareGivenTwice("team"));

    private static ConfiguredShare Accepted(string argument)
    {
        Assert.IsNull(ConfiguredShareReader.Read(argument, out var share));
        return share;
    }
}
