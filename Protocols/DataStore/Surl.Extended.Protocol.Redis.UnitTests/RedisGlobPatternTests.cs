namespace Surl.Extended.Protocol.Redis;

/// <summary>The <c>KEYS</c> page's glob patterns (ADR-0106 decision 1), with Redis's matcher's corners.</summary>
[TestClass]
public sealed class RedisGlobPatternTests
{
    [TestMethod]
    [DataRow("h?llo", "hello")]
    [DataRow("h?llo", "hallo")]
    [DataRow("h*llo", "hllo")]
    [DataRow("h*llo", "heeeello")]
    [DataRow("h[ae]llo", "hallo")]
    [DataRow("h[^e]llo", "hallo")]
    [DataRow("h[a-b]llo", "hbllo")]
    [DataRow("h[b-a]llo", "hallo")]
    [DataRow("*", "")]
    [DataRow("**a**", "xxa")]
    [DataRow("a*b*c", "aXbYbZc")]
    [DataRow(@"h\*llo", "h*llo")]
    [DataRow(@"h[\]]llo", "h]llo")]
    [DataRow(@"x\", @"x\")]
    [DataRow("h[ab", "hb")]
    [DataRow("h[^", "hz")]
    [DataRow("[^]", "q")]
    [DataRow("[a-]", "]")]
    [DataRow(@"[a\", @"\")]
    public void IsMatch_KeyThePatternDescribes_Matches(string pattern, string key)
    {
        var matches = RedisGlobPattern.IsMatch(pattern, key);

        Assert.IsTrue(matches);
    }

    [TestMethod]
    [DataRow("h?llo", "hllo")]
    [DataRow("h[ae]llo", "hillo")]
    [DataRow("h[^e]llo", "hello")]
    [DataRow("h[a-b]llo", "hcllo")]
    [DataRow("hello", "hello!")]
    [DataRow("hello", "hell")]
    [DataRow("a*b", "aXc")]
    [DataRow(@"h\*llo", "hallo")]
    [DataRow("[]", "a")]
    [DataRow("?", "")]
    [DataRow("[a]", "")]
    [DataRow("a[", "ab")]
    [DataRow("[b-c]", "a")]
    public void IsMatch_KeyThePatternDoesNotDescribe_DoesNotMatch(string pattern, string key)
    {
        var matches = RedisGlobPattern.IsMatch(pattern, key);

        Assert.IsFalse(matches);
    }

    [TestMethod]
    public void IsMatch_ManyStarsAgainstALongKey_FinishesByBacktrackingToTheLastStar()
    {
        var pattern = string.Concat(Enumerable.Repeat("a*", 30)) + "b";
        var key = new string('a', 5000);

        var matches = RedisGlobPattern.IsMatch(pattern, key);

        Assert.IsFalse(matches);
    }
}
