namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCasePerlRegexTests
{
    [TestMethod]
    [DataRow(@"^\d+$", "123", true)]
    [DataRow(@"^\d$", "٣", false)]
    [DataRow(@"^\w$", "é", false)]
    [DataRow(@"^\w$", "_", true)]
    [DataRow(@"^\W$", "é", true)]
    [DataRow(@"^\D\S\s$", "a \u000B", true)]
    [DataRow(@"^\S$", "\u0085", true)]
    [DataRow(@"^[\w.]+$", "a.b_1", true)]
    [DataRow(@"^[\d]$", "٣", false)]
    [DataRow(@"^[\s]$", "\f", true)]
    [DataRow(@"^[a]\d$", "a1", true)]
    [DataRow(@"^EPRT \|1\|$", "EPRT |1|", true)]
    [DataRow(@"^a\/b\.c$", "a/b.c", true)]
    public void TryCreate_PerlPattern_MatchesAsPerlDoesOnBytes(string pattern, string line, bool matches)
    {
        var regex = UpstreamTestCasePerlRegex.TryCreate(pattern, ignoreCase: false);

        Assert.IsNotNull(regex);
        Assert.AreEqual(matches, regex.IsMatch(line));
    }

    [TestMethod]
    public void TryCreate_IgnoreCase_MatchesEitherCase()
    {
        var regex = UpstreamTestCasePerlRegex.TryCreate("^gnutls$", ignoreCase: true);

        Assert.IsNotNull(regex);
        Assert.IsTrue(regex.IsMatch("GnuTLS"));
    }

    [TestMethod]
    [DataRow(@"[\S]")]
    [DataRow(@"[\D]")]
    [DataRow(@"[\W]")]
    [DataRow(@"a\K b")]
    [DataRow(@"\Qa\E")]
    [DataRow(@"(unclosed")]
    [DataRow(@"trailing\")]
    public void TryCreate_PatternOutsideTheSubset_IsNull(string pattern) =>
        Assert.IsNull(UpstreamTestCasePerlRegex.TryCreate(pattern, ignoreCase: false));

    [TestMethod]
    public void TryCreate_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCasePerlRegex.TryCreate(null!, ignoreCase: false));
}
