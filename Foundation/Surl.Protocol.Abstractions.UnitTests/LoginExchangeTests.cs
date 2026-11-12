using System.Net;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class LoginExchangeTests
{
    [TestMethod]
    public void Of_AnExchangeContext_CarriesItsIdSchemeAndReportedListenUrl()
    {
        var listenUrl = new ListenUrl("sftp", "127.0.0.1", 0).WithBoundPort(2222);
        var context = new ExchangeContext(
            7,
            listenUrl,
            new IPEndPoint(IPAddress.Loopback, 2222),
            new IPEndPoint(IPAddress.Loopback, 50000),
            new RecordingExchangeLog(),
            TimeProvider.System,
            CancellationToken.None);

        var exchange = LoginExchange.Of(context);

        Assert.AreEqual(7, exchange.ExchangeId);
        Assert.AreEqual("sftp", exchange.Scheme);
        Assert.AreEqual("sftp://127.0.0.1:2222/", exchange.ReportedListenUrl);
    }

    [TestMethod]
    public void Of_NoContext_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LoginExchange.Of(null!));
    }

    [TestMethod]
    public void StartHttpConnection_DefaultWithAnExchange_StartsTheSameSessionAsWithout()
    {
        var policy = new AnonymousAuthenticationPolicy();
        var exchange = new LoginExchange(1, new ListenUrl("http", "127.0.0.1", 80));

        var session = ((IAuthenticationPolicy)policy).StartHttpConnection(null, exchange);

        Assert.AreSame(policy.StartHttpConnection(null), session);
    }
}
