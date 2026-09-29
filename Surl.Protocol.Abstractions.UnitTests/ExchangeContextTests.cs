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

    private static ExchangeContext NewContext() => new(
        1,
        new ListenUrl("http", "localhost", 0).WithBoundPort(8080),
        new IPEndPoint(IPAddress.Loopback, 8080),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        TimeProvider.System,
        CancellationToken.None);
}
