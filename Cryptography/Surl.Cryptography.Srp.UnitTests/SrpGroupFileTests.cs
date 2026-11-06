namespace Surl.Cryptography.Srp;

/// <summary>
/// Checks <see cref="SrpGroupFile.Parse" /> reads GnuTLS <c>tpasswd.conf</c> lines (<c>index:N:g</c>)
/// and refuses a malformed one with <see cref="SrpFileFormatException" /> naming its line (ADR-0122
/// decisions 4 and 6).
/// </summary>
[TestClass]
public sealed class SrpGroupFileTests
{
    internal static string GroupLine(string index, SrpGroup group) =>
        $"{index}:{UnitTestSrpBase64Encoder.Encode(Srp6a.ToUnsignedBigEndian(group.Prime))}:"
            + UnitTestSrpBase64Encoder.Encode(Srp6a.ToUnsignedBigEndian(group.Generator));

    [TestMethod]
    public void Parse_TwoGroupsWithBlankLinesAndCarriageReturns_ReadsBothByIndex()
    {
        var text = $"{GroupLine("1", SrpGroup.Rfc5054Group1024)}\r\n\r\n{GroupLine("3", SrpGroup.Rfc5054Group2048)}\r\n";

        var groups = SrpGroupFile.Parse(text);

        Assert.HasCount(2, groups);
        Assert.AreEqual(SrpGroup.Rfc5054Group1024.Prime, groups["1"].Prime);
        Assert.AreEqual(SrpGroup.Rfc5054Group2048.Prime, groups["3"].Prime);
        Assert.AreEqual(SrpGroup.Rfc5054Group2048.Generator, groups["3"].Generator);
    }

    [TestMethod]
    public void Parse_EmptyText_HasNoGroups()
    {
        var groups = SrpGroupFile.Parse(string.Empty);

        Assert.IsEmpty(groups);
    }

    [TestMethod]
    [DataRow("1:N", 1, "expected 3 fields separated by ':' but found 2")]
    [DataRow("\n1:N:2:3", 2, "expected 3 fields separated by ':' but found 4")]
    [DataRow(":N:2", 1, "the index '' is empty or repeated")]
    [DataRow("1:N:2\n1:N:2", 2, "the index '1' is empty or repeated")]
    [DataRow("1:N=:2", 1, "the prime is not a GnuTLS SRP base64 number")]
    [DataRow("1:N:", 1, "the generator is not a GnuTLS SRP base64 number")]
    [DataRow("1:N:1", 1, "the generator is not greater than one and less than the prime")]
    [DataRow("1:N:N", 1, "the generator is not greater than one and less than the prime")]
    public void Parse_MalformedLine_ThrowsNamingTheLine(string text, int lineNumber, string reason)
    {
        var exception = Assert.ThrowsExactly<SrpFileFormatException>(() => SrpGroupFile.Parse(text));

        Assert.AreEqual(lineNumber, exception.LineNumber);
        Assert.AreEqual(reason, exception.Reason);
        Assert.AreEqual($"line {lineNumber}: {reason}", exception.Message);
    }

    [TestMethod]
    public void Parse_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SrpGroupFile.Parse(null!));
    }
}
