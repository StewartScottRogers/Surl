namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpOAuthFormTests
{
    [TestMethod]
    public void Parse_EncodedPairsAndANameWithoutAValue_AreDecoded()
    {
        IReadOnlyDictionary<string, string>? form = McpOAuthForm.Parse("a=x+y%2Fz&&flag&b=");

        Assert.IsNotNull(form);
        Assert.AreEqual("x y/z", form["a"]);
        Assert.AreEqual(string.Empty, form["flag"]);
        Assert.AreEqual(string.Empty, form["b"]);
        Assert.HasCount(3, form);
    }

    [TestMethod]
    public void Parse_NameGivenTwice_IsNull()
    {
        Assert.IsNull(McpOAuthForm.Parse("a=1&a=2"));
    }

    [TestMethod]
    [DataRow("/authorize", "")]
    [DataRow("/authorize?a=1", "a=1")]
    public void QueryOf_Target_IsItsQueryWithoutTheQuestionMark(string target, string query)
    {
        Assert.AreEqual(query, McpOAuthForm.QueryOf(target));
    }
}
