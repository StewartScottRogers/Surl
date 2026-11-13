using Surl.Extended.Protocol.SignalR;

namespace Surl.Composition;

/// <summary>
/// The SignalR server the extended servers compose for <c>signalr</c> and <c>signalrs</c> (ADR-0119, BL-450):
/// one server claiming both schemes, switched off with the other extended servers.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    [TestMethod]
    public void ComposeExtendedProtocolServers_SwitchOnOrOff_RegistersSignalRAndSignalRsOnlyWhenOn()
    {
        var services = SurlServerComposition.ComposeUnservedExtendedServices(TimeProvider.System);

        var on = SurlServerComposition.ComposeExtendedProtocolServers(true, null, services);
        var off = SurlServerComposition.ComposeExtendedProtocolServers(false, null, services);

        CollectionAssert.AreEqual(new[] { "signalr", "signalrs" }, on.OfType<SignalRHubRoutingProtocolServer>().Single().Schemes.ToArray());
        Assert.IsFalse(off.Any(server => server.Schemes.Contains("signalr") || server.Schemes.Contains("signalrs")));
    }
}

[TestClass]
public sealed class SignalRHubRoutingProtocolServerTests
{
    private static SignalRHubRoutingProtocolServer Create() => new(hubPath => new SignalRProtocolServer(
        hubPath, new SignalRHubConnections(TimeProvider.System), new Surl.Protocol.Abstractions.AnonymousAuthenticationPolicy()));

    [TestMethod]
    public void ServerFor_ListenUrlWithPath_ServesTheHubAtThatPathAndOneHubPerPath()
    {
        var router = Create();
        var chat = new Surl.Protocol.Abstractions.ListenUrl("signalr", "127.0.0.1", 0) { Path = "/chat" };
        var chatSlash = chat with { Path = "/chat/" };
        var other = chat with { Path = "/other" };
        var root = new Surl.Protocol.Abstractions.ListenUrl("signalr", "127.0.0.1", 0);

        Assert.AreEqual("/chat", SignalRHubRoutingProtocolServer.HubPathFor(chat).EndpointBase);
        Assert.AreEqual("/", SignalRHubRoutingProtocolServer.HubPathFor(root).EndpointBase);
        Assert.AreSame(router.ServerFor(chat), router.ServerFor(chatSlash));
        Assert.AreNotSame(router.ServerFor(chat), router.ServerFor(other));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SignalRHubRoutingProtocolServer(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => SignalRHubRoutingProtocolServer.HubPathFor(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => router.ServeAsync(null!, null!));
    }
}
