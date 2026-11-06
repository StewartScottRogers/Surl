using static Surl.Cryptography.Srp.Rfc5054AppendixB;

namespace Surl.Cryptography.Srp;

/// <summary>
/// Checks <see cref="SrpVerifierFile.Parse" /> reads GnuTLS <c>tpasswd</c> lines
/// (<c>user:verifier:salt:index</c>) into accounts whose verifier and salt are RFC 5054 Appendix B's,
/// and refuses a malformed line with <see cref="SrpFileFormatException" /> naming it (ADR-0122
/// decisions 4 and 6).
/// </summary>
[TestClass]
public sealed class SrpVerifierFileTests
{
    private static readonly IReadOnlyDictionary<string, SrpGroup> Groups =
        SrpGroupFile.Parse(SrpGroupFileTests.GroupLine("1", SrpGroup.Rfc5054Group1024));

    private static string AliceLine =>
        $"alice:{UnitTestSrpBase64Encoder.Encode(Convert.FromHexString(VerifierHex))}:{UnitTestSrpBase64Encoder.Encode(Salt)}:1";

    [TestMethod]
    public void Parse_AppendixBAccount_ReadsUserSaltVerifierAndGroup()
    {
        var entries = SrpVerifierFile.Parse($"{AliceLine}\r\n", Groups);

        var entry = entries.Single();
        Assert.AreEqual("alice", entry.UserName);
        Assert.AreEqual(SaltHex, Convert.ToHexString(entry.Salt));
        Assert.AreEqual(VerifierHex, Convert.ToHexString(entry.Verifier));
        Assert.AreEqual(SrpGroup.Rfc5054Group1024.Prime, entry.Group.Prime);
    }

    [TestMethod]
    public void Parse_AppendixBAccount_VerifierMatchesThePasswordsVerifier()
    {
        var entry = SrpVerifierFile.Parse(AliceLine, Groups).Single();

        var verifier = Srp6a.ComputeVerifier(entry.Group, entry.Salt, UserName, Password);

        CollectionAssert.AreEqual(entry.Verifier, Srp6a.ToUnsignedBigEndian(verifier));
    }

    [TestMethod]
    public void Parse_TwoAccounts_KeepsFileOrder()
    {
        var entries = SrpVerifierFile.Parse($"{AliceLine}\n\n{AliceLine.Replace("alice", "jsmith", StringComparison.Ordinal)}", Groups);

        CollectionAssert.AreEqual(new[] { "alice", "jsmith" }, entries.Select(entry => entry.UserName).ToArray());
    }

    [TestMethod]
    [DataRow("alice:v:s", 1, "expected 4 fields separated by ':' but found 3")]
    [DataRow("\n\nalice:v:s:1:x", 3, "expected 4 fields separated by ':' but found 5")]
    [DataRow(":v:s:1", 1, "the user name is empty")]
    [DataRow("alice:v=:s:1", 1, "the verifier is not a GnuTLS SRP base64 number")]
    [DataRow("alice:v::1", 1, "the salt is not a GnuTLS SRP base64 number")]
    [DataRow("alice:v:s:2", 1, "the group index '2' is not in the groups file")]
    public void Parse_MalformedLine_ThrowsNamingTheLine(string text, int lineNumber, string reason)
    {
        var exception = Assert.ThrowsExactly<SrpFileFormatException>(() => SrpVerifierFile.Parse(text, Groups));

        Assert.AreEqual(lineNumber, exception.LineNumber);
        Assert.AreEqual(reason, exception.Reason);
    }

    [TestMethod]
    public void Parse_NullTextOrGroups_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SrpVerifierFile.Parse(null!, Groups));
        Assert.ThrowsExactly<ArgumentNullException>(() => SrpVerifierFile.Parse(AliceLine, null!));
    }
}
