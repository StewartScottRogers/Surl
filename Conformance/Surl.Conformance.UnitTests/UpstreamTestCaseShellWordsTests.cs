namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseShellWordsTests
{
    [TestMethod]
    public void Split_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseShellWords.Split(null!));
    }

    [TestMethod]
    public void Split_BlanksAndTabs_SeparateWordsAndRunsOfThemCountOnce()
    {
        CollectionAssert.AreEqual(
            new[] { "http://127.0.0.1:8990/1", "-H", "X:" },
            Split("  http://127.0.0.1:8990/1 \t -H\tX:  "));
    }

    [TestMethod]
    public void Split_Empty_HasNoWords()
    {
        Assert.IsEmpty(UpstreamTestCaseShellWords.Split(" "));
    }

    [TestMethod]
    public void Split_SingleQuotes_KeepEverythingLiterally()
    {
        CollectionAssert.AreEqual(new[] { "a \"b\" \\c $d", "e" }, Split("'a \"b\" \\c $d' e"));
    }

    [TestMethod]
    public void Split_EmptyQuotes_AreAnEmptyWord()
    {
        CollectionAssert.AreEqual(new[] { "-H", string.Empty, "x" }, Split("-H '' x"));
    }

    [TestMethod]
    public void Split_DoubleQuotes_KeepBlanksAndUnescapeOnlyDollarBacktickQuoteAndBackslash()
    {
        CollectionAssert.AreEqual(
            new[] { "a b $ ` \" \\ \\n" },
            Split("\"a b \\$ \\` \\\" \\\\ \\n\""));
    }

    [TestMethod]
    public void Split_EscapedBackslashBeforeTheClosingDoubleQuote_IsOneBackslashAndTheQuoteCloses()
    {
        CollectionAssert.AreEqual(new[] { "a\\" }, Split("\"a\\\\\""));
    }

    [TestMethod]
    public void Split_QuotedAndUnquotedRunsTouching_AreOneWord()
    {
        CollectionAssert.AreEqual(new[] { "X-Header: it's" }, Split("X-Header:\" it\"\\''s'"));
    }

    [TestMethod]
    public void Split_BackslashOutsideQuotes_StandsForTheNextCharacter()
    {
        CollectionAssert.AreEqual(new[] { "a b", "\"" }, Split("a\\ b \\\""));
    }

    [TestMethod]
    public void Split_BackslashEndingTheLine_StandsForItself()
    {
        CollectionAssert.AreEqual(new[] { "a\\" }, Split("a\\"));
    }

    [TestMethod]
    public void Split_UnclosedSingleQuote_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => Split("a 'b"));
    }

    [TestMethod]
    public void Split_UnclosedDoubleQuote_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => Split("a \"b\\\""));
    }

    [TestMethod]
    public void Split_DoubleQuoteLeftOpenAfterABackslash_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => Split("\"a\\"));
    }

    private static string[] Split(string commandLine) => [.. UpstreamTestCaseShellWords.Split(commandLine)];
}
