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
}
