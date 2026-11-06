using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Core.InMemoryConnectionWaits;

namespace Surl.Core;

// Whether a connection starts with a TLS handshake follows its listen URL's IsImplicitTls,
// not its scheme alone, so one s3 listener can be plaintext beside another over TLS (BL-1020).
public sealed partial class ServingEngineTests
{
    private static readonly ListenUrl PlainS3 = new("s3", "127.0.0.1", 9000) { IsImplicitTls = false };
    private static readonly ListenUrl SecureS3 = new("s3", "127.0.0.1", 9443);

    [TestMethod]
    public async Task ServeAsync_S3ListenUrlSaysPlain_ServesWithoutAHandshake_WhileTheDefaultS3ListenUrlHandshakes()
    {
        var factory = new FakeListenerFactory();
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer("s3");
        var engine = CreateEngine(factory, new ManualTimeProvider(), logs, server);
        var plain = new InMemoryConnection([]);
        var secure = new InMemoryConnection([]);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([PlainS3, SecureS3], stop.Token);

        factory.ListenerFor(PlainS3).Connect(plain);
        var plainExchange = await server.NextExchangeAsync();
        factory.ListenerFor(SecureS3).Connect(secure);
        var secureExchange = await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.IsFalse(plain.UpgradeRequested);
        Assert.IsNull(plainExchange.Connection.TlsSession);
        Assert.IsTrue(secure.UpgradeRequested);
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, secureExchange.Connection.TlsSession);
    }

    [TestMethod]
    public async Task ServeAsync_PlainS3ListenUrl_AnswersAConnectionPastTheLimitWithTheServersRefusal()
    {
        using var harness = new Harness(OneConnection, "s3", listenUrl: PlainS3);
        var admitted = HeldConnection(PeerAddress(1));
        harness.Listener.Connect(admitted);
        await harness.Server.Inner.NextExchangeAsync();

        var refused = HeldConnection(PeerAddress(2));
        harness.Listener.Connect(refused);
        var refusal = await harness.Server.NextRefusalAsync();
        await WaitUntilDisposedAsync(refused);

        Assert.IsFalse(admitted.UpgradeRequested);
        Assert.AreEqual(ConnectionRefusal.TooManyConnections, refusal.Refusal);
        Assert.AreEqual("refused:TooManyConnections", Encoding.ASCII.GetString(refused.WrittenBytes));
        Assert.IsFalse(refused.UpgradeRequested);
        await harness.StopAsync();
    }

    [TestMethod]
    public async Task ServeAsync_DefaultS3ListenUrl_ClosesAConnectionPastTheLimitBeforeTheHandshake()
    {
        using var harness = new Harness(OneConnection, "s3", listenUrl: SecureS3);
        var admitted = HeldConnection(PeerAddress(1));
        harness.Listener.Connect(admitted);
        await harness.Server.Inner.NextExchangeAsync();

        var refused = HeldConnection(PeerAddress(2));
        harness.Listener.Connect(refused);
        await WaitUntilDisposedAsync(refused);

        Assert.IsTrue(admitted.UpgradeRequested);
        Assert.IsFalse(harness.Server.TryTakeRefusal(out _));
        Assert.IsEmpty(refused.WrittenBytes);
        Assert.IsFalse(refused.UpgradeRequested);
        await harness.StopAsync();
    }
}
