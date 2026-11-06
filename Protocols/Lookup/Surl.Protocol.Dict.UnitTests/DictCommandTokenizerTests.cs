namespace Surl.Protocol.Dict;

[TestClass]
public sealed class DictCommandTokenizerTests
{
    [TestMethod]
    [DataRow("DEFINE ! hello", new[] { "DEFINE", "!", "hello" })]
    [DataRow("  MATCH\t! .   hel  ", new[] { "MATCH", "!", ".", "hel" })]
    [DataRow("DEFINE db a\\ b\\\"c", new[] { "DEFINE", "db", "a b\"c" })]
    [DataRow("DEFINE ! \"two words\"", new[] { "DEFINE", "!", "two words" })]
    [DataRow("DEFINE ! 'it\"s'", new[] { "DEFINE", "!", "it\"s" })]
    [DataRow("DEFINE ! pre\"fix ed\"post", new[] { "DEFINE", "!", "prefix edpost" })]
    [DataRow("DEFINE ! \"a\\\"b\"", new[] { "DEFINE", "!", "a\"b" })]
    [DataRow("DEFINE ! \"\"", new[] { "DEFINE", "!", "" })]
    [DataRow("DEFINE ! \\\\", new[] { "DEFINE", "!", "\\" })]
    public void Split_CommandLine_ReturnsItsWords(string line, string[] expected)
    {
        var words = DictCommandTokenizer.Split(line);

        Assert.IsNotNull(words);
        CollectionAssert.AreEqual(expected, words.ToArray());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" \t ")]
    public void Split_BlankLine_ReturnsNoWords(string line)
    {
        var words = DictCommandTokenizer.Split(line);

        Assert.IsNotNull(words);
        Assert.IsEmpty(words);
    }

    [TestMethod]
    [DataRow("DEFINE ! \"open")]
    [DataRow("DEFINE ! 'open")]
    [DataRow("DEFINE ! end\\")]
    public void Split_UnclosedQuoteOrLoneTrailingBackslash_ReturnsNull(string line)
    {
        Assert.IsNull(DictCommandTokenizer.Split(line));
    }
}
