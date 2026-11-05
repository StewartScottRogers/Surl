using System.Net;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class ExchangeContextTests
{
    [TestMethod]
    public void Scheme_AnyContext_IsTheListenUrlsScheme()
    {
        var listenUrl = new ListenUrl("https", "localhost", 0).WithBoundPort(8443);
        var context = new ExchangeContext(
            1,
            listenUrl,
            new IPEndPoint(IPAddress.Loopback, 8443),
            new IPEndPoint(IPAddress.Loopback, 50000),
            new RecordingExchangeLog(),
            TimeProvider.System,
            CancellationToken.None);

        var scheme = context.Scheme;

        Assert.AreEqual("https", scheme);
    }

    [TestMethod]
    public void Limits_DefaultsToExchangeLimitsDefault()
    {
        var context = NewContext();

        Assert.AreSame(ExchangeLimits.Default, context.Limits);
    }

    [TestMethod]
    public void Limits_CarriesTheLimitsItWasGiven()
    {
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(5), 1, 2, 3, 4);

        var context = NewContext() with { Limits = limits };

        Assert.AreSame(limits, context.Limits);
    }

    [TestMethod]
    public void ShutdownToken_DefaultsToNone()
    {
        var context = NewContext();

        Assert.AreEqual(CancellationToken.None, context.ShutdownToken);
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    public void IsCancelledForALimit_IsTheExchangeCancelledWhileShutdownIsNot(
        bool isExchangeCancelled, bool isShutdown, bool expected)
    {
        var context = NewContext(new CancellationToken(isExchangeCancelled)) with
        {
            ShutdownToken = new CancellationToken(isShutdown),
        };

        Assert.AreEqual(expected, context.IsCancelledForALimit);
    }

    [TestMethod]
    public void RemoteEndPoint_UnnamedUnixSocketPeer_IsAUnixPeerWithNoIpAddress()
    {
        var listenUrl = new ListenUrl("http", "localhost", 80)
        {
            UnixSocket = new UnixSocketAddress("/tmp/surl.sock", false),
        };

        var context = new ExchangeContext(
            1,
            listenUrl,
            new IPEndPoint(IPAddress.Loopback, 80),
            UnnamedUnixSocketPeer.Instance,
            new RecordingExchangeLog(),
            TimeProvider.System,
            CancellationToken.None);

        Assert.AreSame(UnnamedUnixSocketPeer.Instance, context.RemoteEndPoint);
        Assert.AreEqual(System.Net.Sockets.AddressFamily.Unix, context.RemoteEndPoint.AddressFamily);
        Assert.IsNotInstanceOfType<IPEndPoint>(context.RemoteEndPoint);
        Assert.AreEqual("unix peer", context.RemoteEndPoint.ToString());
        Assert.AreEqual("http", context.Scheme);
    }

    private static ExchangeContext NewContext() => NewContext(CancellationToken.None);

    private static ExchangeContext NewContext(CancellationToken cancellationToken) => new(
        1,
        new ListenUrl("http", "localhost", 0).WithBoundPort(8080),
        new IPEndPoint(IPAddress.Loopback, 8080),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        TimeProvider.System,
        cancellationToken);
}
