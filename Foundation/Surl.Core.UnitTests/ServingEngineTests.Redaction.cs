using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

// A server that implements IExchangeBytesRedactingServer has the level's log fed redacted received
// bytes while the event stream and the wire counts keep the wire (ADR-0182, decisions 2, 4, 6; BL-1419).
public sealed partial class ServingEngineTests
{
    private static readonly ReadOnlyMemory<byte>[] SecretChunks = ["pass sec"u8.ToArray(), "ret\ntail secret"u8.ToArray()];

    [TestMethod]
    public async Task ServeAsync_RedactingServer_LevelLogReceivesRedactedBytes_AndExchangeEndedCountsWireBytes()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions());
        var (logs, _) = await ServeSecretExchangeAsync(stream);

        var received = logs.LogOf(1).Entries.Where(entry => entry.Kind == ExchangeLogEntryKind.BytesReceived)
            .SelectMany(entry => entry.Bytes).ToArray();
        Assert.AreEqual("pass ****\ntail ****", Encoding.ASCII.GetString(received));
        var ended = Drain(subscription).OfType<ExchangeEndedEvent>().Single();
        Assert.AreEqual((long)SecretChunks.Sum(chunk => chunk.Length), ended.ReceivedByteCount);
    }

    [TestMethod]
    public async Task ServeAsync_RedactingServer_ExchangeBytesEventCarriesTheRawWireBytes()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });
        _ = await ServeSecretExchangeAsync(stream);

        var raw = Drain(subscription).OfType<ExchangeBytesEvent>().Where(e => e.Direction == ExchangeDirection.Received)
            .SelectMany(e => e.Bytes.ToArray()).ToArray();
        Assert.AreEqual("pass secret\ntail secret", Encoding.ASCII.GetString(raw));
    }

    [TestMethod]
    public async Task ServeAsync_RedactingServer_IsAskedForARedactorWithTheSchemeAndExchangeId_AndSentBytesAreObserved()
    {
        var (_, server) = await ServeSecretExchangeAsync(new SurlEventStream(new ManualTimeProvider()));

        Assert.AreEqual(new ExchangeRedactionContext(1, "http"), server.Requests.First());
        CollectionAssert.AreEqual(new[] { "334 secret" }, server.Sent);
    }

    [TestMethod]
    public async Task ServeAsync_RedactingServer_DecryptedBytesNoteIsRedactedAndTheEventIsRaw()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });
        var logs = new FakeExchangeLogFactory();
        var server = new FakeRedactingConnectionServer(
            (connection, context) =>
            {
                context.DecryptedBytes.Received(ExchangeBytesLayer.Decrypted, 4, "pw secret\n"u8);
                context.DecryptedBytes.Sent(ExchangeBytesLayer.Decoded, null, "334 secret"u8);
                return Task.CompletedTask;
            },
            "http");
        await ServeOneAsync(server, logs, stream, new InMemoryConnection([]));

        var notes = logs.LogOf(1).Entries.Select(entry => entry.Text).Where(text => text.Contains("bytes ")).ToArray();
        StringAssert.EndsWith(notes[0], "8 bytes: " + Convert.ToHexString("pw ****\n"u8));
        StringAssert.EndsWith(notes[1], "10 bytes: " + Convert.ToHexString("334 secret"u8));
        var events = Drain(subscription).OfType<ExchangeBytesEvent>().ToArray();
        CollectionAssert.AreEqual("pw secret\n"u8.ToArray(), events[0].Bytes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_RedactingServer_DataConnectionBytesAreLoggedRawAndCounted()
    {
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions());
        var logs = new FakeExchangeLogFactory();
        var opener = new InMemoryDataConnections().ScriptActiveConnection(
            new InMemoryConnection(["secret\n"u8.ToArray()], remoteEndPoint: CurlDataPort));
        var server = new FakeRedactingConnectionServer(
            async (connection, context) =>
            {
                await using var data = await context.DataConnections.ConnectActiveAsync(
                    connection.RemoteEndPoint, CurlDataPort, TimeSpan.FromSeconds(30), context.CancellationToken);
                await data.WriteAsync("secret out"u8.ToArray(), context.CancellationToken);
                await data.ReadAsync(new byte[16], context.CancellationToken);
            },
            "ftp");
        var factory = new FakeListenerFactory();
        using var stop = new CancellationTokenSource();
        var serving = new ServingEngine(
                factory, [server], logs, new ManualTimeProvider(), GracePeriod, ConnectionLimits.Default, opener, ExchangeLimits.Default, stream)
            .ServeAsync([Ftp], stop.Token);
        var control = new InMemoryConnection([], remoteEndPoint: PeerAddress(1));

        factory.ListenerFor(Ftp).Connect(control);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(control);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        var entries = logs.LogOf(1).Entries;
        CollectionAssert.AreEqual("secret\n"u8.ToArray(), entries.Single(e => e.Kind == ExchangeLogEntryKind.BytesReceived).Bytes);
        CollectionAssert.AreEqual("secret out"u8.ToArray(), entries.Single(e => e.Kind == ExchangeLogEntryKind.BytesSent).Bytes);
        var ended = Drain(subscription).OfType<ExchangeEndedEvent>().Single();
        Assert.AreEqual((7L, 10L), (ended.ReceivedByteCount, ended.SentByteCount));
    }

    private static async Task<(FakeExchangeLogFactory Logs, FakeRedactingConnectionServer Server)> ServeSecretExchangeAsync(SurlEventStream stream)
    {
        var logs = new FakeExchangeLogFactory();
        var server = new FakeRedactingConnectionServer(
            async (connection, context) =>
            {
                var buffer = new byte[64];
                while (await connection.ReadAsync(buffer, context.CancellationToken) > 0)
                {
                }

                await connection.WriteAsync("334 secret"u8.ToArray(), context.CancellationToken);
            },
            "http");
        await ServeOneAsync(server, logs, stream, new InMemoryConnection(SecretChunks));

        return (logs, server);
    }

    private static async Task ServeOneAsync(
        IConnectionProtocolServer server, FakeExchangeLogFactory logs, SurlEventStream stream, InMemoryConnection connection)
    {
        var factory = new FakeListenerFactory();
        var engine = new ServingEngine(
            factory, [server], logs, new ManualTimeProvider(), GracePeriod, ConnectionLimits.None,
            RefusingDataConnectionOpener.Instance, ExchangeLimits.Default, stream);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(connection);
        await InMemoryConnectionWaits.WaitUntilDisposedAsync(connection);
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);
    }
}
