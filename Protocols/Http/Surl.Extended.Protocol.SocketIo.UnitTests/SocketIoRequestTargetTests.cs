namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// A request target's path and query parameters (EIO "Request path", "Query parameters").
/// </summary>
[TestClass]
public sealed class SocketIoRequestTargetTests
{
    [TestMethod]
    public void Parse_OriginForm_SplitsThePathAndReadsDecodedParameters()
    {
        var target = SocketIoRequestTarget.Parse("/socket.io/?EIO=4&transport=websocket&t%20x=a+b%2Fc&flag");

        Assert.AreEqual("/socket.io/", target.Path);
        Assert.AreEqual("4", target.QueryParameter("EIO"));
        Assert.AreEqual("a b/c", target.QueryParameter("t x"));
        Assert.AreEqual(string.Empty, target.QueryParameter("flag"));
        Assert.IsNull(target.QueryParameter("sid"));
    }

    [TestMethod]
    public void Parse_AbsoluteFormWithoutQuery_IsItsPathWithNoParameters()
    {
        var target = SocketIoRequestTarget.Parse("http://127.0.0.1:5000/socket.io/");

        Assert.AreEqual("/socket.io/", target.Path);
        Assert.IsNull(target.QueryParameter("EIO"));
    }
}
