namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The hub's <c>[endpoint-base]</c> and negotiate path from the listen URL's path (ADR-0119, decision 1).
/// </summary>
[TestClass]
public sealed class SignalRHubPathTests
{
    [TestMethod]
    [DataRow("/chat", "/chat", "/chat/negotiate")]
    [DataRow("/chat/", "/chat", "/chat/negotiate")]
    [DataRow("/a/b//", "/a/b", "/a/b/negotiate")]
    [DataRow("", "/", "/negotiate")]
    [DataRow("/", "/", "/negotiate")]
    public void FromListenPath_Path_GivesTheEndpointBaseAndNegotiatePath(string listenPath, string endpointBase, string negotiatePath)
    {
        var hubPath = SignalRHubPath.FromListenPath(listenPath);

        Assert.AreEqual(endpointBase, hubPath.EndpointBase);
        Assert.AreEqual(negotiatePath, hubPath.NegotiatePath);
    }

    [TestMethod]
    public void FromListenPath_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => SignalRHubPath.FromListenPath(null!));
    }
}
