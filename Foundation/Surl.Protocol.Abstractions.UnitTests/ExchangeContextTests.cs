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
