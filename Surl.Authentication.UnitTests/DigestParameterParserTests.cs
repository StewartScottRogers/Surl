namespace Surl.Authentication;

/// <summary>
/// <see cref="DigestParameterParser"/>: RFC 9110 section 11.2's <c>auth-param</c> list.
/// </summary>
[TestClass]
public sealed class DigestParameterParserTests
{
    [TestMethod]
    public void Parse_CurlsAnswer_ReadsEveryParameter()
    {
        var parameters = DigestParameterParser.Parse(
            "username=\"a\",realm=\"r\",nonce=\"n\",uri=\"/x?y=1,2\",nc=00000001,algorithm=MD5,qop=\"auth\"");

        Assert.IsNotNull(parameters);
        Assert.HasCount(7, parameters);
        Assert.AreEqual("/x?y=1,2", parameters["URI"]);
        Assert.AreEqual("00000001", parameters["nc"]);
        Assert.AreEqual("MD5", parameters["algorithm"]);
    }

    [TestMethod]
    public void Parse_SpacesTabsAndEmptyElements_AreSkipped()
    {
        var parameters = DigestParameterParser.Parse(" ,a = \"1\" ,\t, b\t=\t2 , ");

        Assert.IsNotNull(parameters);
        Assert.AreEqual("1", parameters["a"]);
        Assert.AreEqual("2", parameters["b"]);
    }

    [TestMethod]
    public void Parse_EscapedQuoteAndBackslash_AreUnescaped()
    {
        var parameters = DigestParameterParser.Parse("username=\"te\\\"st\\\\\", e=\"\"");

        Assert.IsNotNull(parameters);
        Assert.AreEqual("te\"st\\", parameters["username"]);
        Assert.AreEqual(string.Empty, parameters["e"]);
    }

    [TestMethod]
    public void Parse_Empty_IsNoParameters()
    {
        Assert.IsEmpty(DigestParameterParser.Parse(string.Empty)!);
    }

    [TestMethod]
    [DataRow("username", DisplayName = "no =")]
    [DataRow("username ", DisplayName = "no = before the end")]
    [DataRow("username=", DisplayName = "no value")]
    [DataRow("username=,a=b", DisplayName = "empty token")]
    [DataRow("=a", DisplayName = "no name")]
    [DataRow("username=\"a", DisplayName = "unterminated")]
    [DataRow("username=\"a\\", DisplayName = "unterminated after a backslash")]
    [DataRow("username=\"a\"b=c", DisplayName = "no separator after a quoted value")]
    [DataRow("a=b\"c\"", DisplayName = "quote inside a token")]
    [DataRow("a=1, A=2", DisplayName = "a name twice")]
    [DataRow("a b=1", DisplayName = "a name with no =")]
    public void Parse_Malformed_IsNull(string credentials)
    {
        Assert.IsNull(DigestParameterParser.Parse(credentials));
    }
}
