using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Reads LDIF into records by ADR-0072 decision 1's rules, and refuses each malformed file with
/// the line and the text the decision names.
/// </summary>
[TestClass]
public sealed class LdifReaderTests
{
    [TestMethod]
    public void Read_NoBytes_HasNoRecords()
    {
        Assert.IsEmpty(LdifReader.Read([]));
    }

    [TestMethod]
    [DataRow("version: 1\n")]
    [DataRow("# only a comment\n continued\n\n\n")]
    [DataRow("\n\n")]
    public void Read_NoRecord_HasNoRecords(string ldif)
    {
        Assert.IsEmpty(Read(ldif));
    }

    [TestMethod]
    public void Read_VersionLineThenARecordWithNoEmptyLineBetween_ReadsTheRecord()
    {
        var records = Read("Version:1\r\ndn: o=a\r\no: a");

        Assert.AreEqual("o=a", records.Single().Entry.Dn.Text);
        Assert.AreEqual(2, records[0].Line);
    }

    [TestMethod]
    public void Read_RecordsSeparatedByEmptyLinesAndComments_KeepsFileOrderAndLines()
    {
        var records = Read("# head\ndn: o=a\no: a\n\n\n# between\n\ndn: ou=b,o=a\nou: b\n");

        CollectionAssert.AreEqual(new[] { "o=a", "ou=b,o=a" }, records.Select(record => record.Entry.Dn.Text).ToArray());
        CollectionAssert.AreEqual(new[] { 2, 8 }, records.Select(record => record.Line).ToArray());
    }

    [TestMethod]
    public void Read_ACommentInsideARecord_IsDropped()
    {
        var entry = Read("dn: o=a\n# a comment\n with its continuation\no: a\n").Single().Entry;

        Assert.AreEqual("o=a", Text(entry));
    }

    [TestMethod]
    public void Read_RepeatedDescription_AddsValuesInFileOrderToTheFirstAttribute()
    {
        var entry = Read("dn: o=a\nobjectClass: top\no: a\nOBJECTCLASS: organization\n").Single().Entry;

        Assert.AreEqual("objectClass=top,organization;o=a", Text(entry));
    }

    [TestMethod]
    public void Read_FoldedLines_AreUnfoldedWithTheLeadingSpaceDropped()
    {
        var entry = Read("dn: o=a\ndescrip\n tion: one\n  two\n").Single().Entry;

        Assert.AreEqual("description=one two", Text(entry));
    }

    [TestMethod]
    public void Read_DescriptionsWithOptionsAndNumericOids_AreKeptAsWritten()
    {
        var entry = Read("dn: o=a\ncn;lang-en: x\n2.5.4.10: y\n1.0.3: z\n").Single().Entry;

        Assert.AreEqual("cn;lang-en=x;2.5.4.10=y;1.0.3=z", Text(entry));
    }

    [TestMethod]
    public void Read_EmptyAndBase64Values_AreDecoded()
    {
        var entry = Read("dn:: bz1hw6k=\ndescription:\nuserPassword:: \ncn::  w6k=\nsn: has  inner and trailing spaces  \n").Single().Entry;

        Assert.AreEqual("o=aé", entry.Dn.Text);
        Assert.AreEqual("description=;userPassword=;cn=é;sn=has  inner and trailing spaces  ", Text(entry));
    }

    [TestMethod]
    public void Read_ByteOrderMark_IsNotUtf8OnLineOne()
    {
        AssertRefused([0xEF, 0xBB, 0xBF, .. "dn: o=a\no: a\n"u8], 1, LdifFaultText.NotUtf8);
    }

    [TestMethod]
    public void Read_InvalidUtf8_IsNotUtf8OnItsLine()
    {
        AssertRefused([.. "dn: o=a\r\n# caf"u8, 0xE9, .. "\r\no: a\r\n"u8], 2, LdifFaultText.NotUtf8);
    }

    [TestMethod]
    [DataRow("version: 2\n\ndn: o=a\no: a\n", 1, LdifFaultText.VersionNotOne)]
    [DataRow("version: 1 \ndn: o=a\no: a\n", 1, LdifFaultText.VersionNotOne)]
    [DataRow(" dn: o=a\no: a\n", 1, LdifFaultText.ContinuationWithNothing)]
    [DataRow("dn: o=a\no: a\n\n continued\n", 4, LdifFaultText.ContinuationWithNothing)]
    [DataRow("dn: o=a\no: a\n\no: b\n", 4, LdifFaultText.RecordWithoutDn)]
    [DataRow("dn: o=a\no: a\n\nversion: 1\n", 4, LdifFaultText.RecordWithoutDn)]
    [DataRow("dn: not a dn\no: a\n", 1, LdifFaultText.DnNotRfc4514)]
    [DataRow("dn:: //79\no: a\n", 1, LdifFaultText.DnNotRfc4514)]
    [DataRow("dn: o=a\nno colon here\n", 2, LdifFaultText.NotDescriptionValue)]
    [DataRow("dn: o=a\n: x\n", 2, LdifFaultText.NotDescriptionValue)]
    [DataRow("dn: o=a\n1abc: x\n", 2, LdifFaultText.NotDescriptionValue)]
    [DataRow("dn: o=a\nc_n: x\n", 2, LdifFaultText.NotDescriptionValue)]
    [DataRow("dn: o=a\n1: x\n", 2, LdifFaultText.NotDescriptionValue)]
    [DataRow("dn: o=a\n1..2: x\n", 2, LdifFaultText.NotDescriptionValue)]
    [DataRow("dn: o=a\n1.a: x\n", 2, LdifFaultText.NotDescriptionValue)]
    [DataRow("dn: o=a\n1.02: x\n", 2, LdifFaultText.NotDescriptionValue)]
    [DataRow("dn: o=a\ncn;: x\n", 2, LdifFaultText.NotDescriptionValue)]
    [DataRow("dn: o=a\ncn;lang_en: x\n", 2, LdifFaultText.NotDescriptionValue)]
    [DataRow("dn: o=a\ncn:: abc!\n", 2, LdifFaultText.BadBase64)]
    [DataRow("dn: o=a\ncn:: abc\n", 2, LdifFaultText.BadBase64)]
    [DataRow("dn: o=a\ncn:: YWJj \n", 2, LdifFaultText.BadBase64)]
    [DataRow("dn: o=a\ncn: café\n", 2, LdifFaultText.NotSafeString)]
    [DataRow("dn: o=a\ncn: :x\n", 2, LdifFaultText.NotSafeString)]
    [DataRow("dn: o=a\ncn: <x\n", 2, LdifFaultText.NotSafeString)]
    [DataRow("dn: o=a\ncn: a\0b\n", 2, LdifFaultText.NotSafeString)]
    [DataRow("dn: o=a\ncn: a\rb\n", 2, LdifFaultText.NotSafeString)]
    [DataRow("dn: o=a\nchangetype: add\no: a\n", 2, LdifFaultText.ChangeRecord)]
    [DataRow("dn: o=a\no:< file:///etc/passwd\n", 2, LdifFaultText.UrlValue)]
    public void Read_MalformedFile_IsRefusedWithItsLineAndText(string ldif, int line, string what)
    {
        AssertRefused(Encoding.UTF8.GetBytes(ldif), line, what);
    }

    private static IReadOnlyList<LdifRecord> Read(string ldif) => LdifReader.Read(Encoding.UTF8.GetBytes(ldif));

    private static string Text(LdapEntry entry) =>
        string.Join(';', entry.Attributes.Select(attribute => $"{attribute.Description.Text}={string.Join(',', attribute.Values.Select(Encoding.UTF8.GetString))}"));

    private static void AssertRefused(byte[] ldif, int line, string what)
    {
        var exception = Assert.ThrowsExactly<LdifFormatException>(() => LdifReader.Read(ldif));

        Assert.AreEqual(line, exception.Line);
        Assert.AreEqual($"line {line}: {what}", exception.Message);
    }
}
