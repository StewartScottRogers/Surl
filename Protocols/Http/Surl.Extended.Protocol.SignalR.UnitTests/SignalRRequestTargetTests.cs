namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A request target's path and query parameters (ADR-0119, decisions 1, 2 and 5).
/// </summary>
[TestClass]
public sealed class SignalRRequestTargetTests
{
    [TestMethod]
    [DataRow("/chat/negotiate?negotiateVersion=1", "/chat/negotiate")]
    [DataRow("/chat", "/chat")]
    [DataRow("/chat%2Fx?", "/chat%2Fx")]
    [DataRow("http://example.com/chat/negotiate?negotiateVersion=1", "/chat/negotiate")]
    [DataRow("*", "*")]
    public void Parse_Target_GivesItsPath(string target, string path)
    {
        Assert.AreEqual(path, SignalRRequestTarget.Parse(target).Path);
    }

    [TestMethod]
    [DataRow("/h?negotiateVersion=1", "negotiateVersion", "1")]
    [DataRow("/h?a=1&negotiateVersion=2&negotiateVersion=3", "negotiateVersion", "2")]
    [DataRow("/h?access_token=a%2Bb+c", "access_token", "a+b c")]
    [DataRow("/h?access%5Ftoken=x", "access_token", "x")]
    [DataRow("/h?flag", "flag", "")]
    [DataRow("/h?flag=", "flag", "")]
    [DataRow("http://example.com/h?id=7", "id", "7")]
    public void QueryParameter_Present_IsItsDecodedValue(string target, string name, string value)
    {
        Assert.AreEqual(value, SignalRRequestTarget.Parse(target).QueryParameter(name));
    }

    [TestMethod]
    [DataRow("/h")]
    [DataRow("/h?other=1")]
    [DataRow("/h?negotiateVersionX=1")]
    public void QueryParameter_Absent_IsNull(string target)
    {
        Assert.IsNull(SignalRRequestTarget.Parse(target).QueryParameter("negotiateVersion"));
    }
}
