using Surl.Protocol.Abstractions;

namespace Surl.Core;

// The engine hands each server a sink for the bytes its code, such as DTLS, QUIC or HTTP/3 code, holds in the
// clear (ADR-0169; BL-1267).
public sealed partial class ServingEngineTests
{
    [TestMethod]
    public async Task ServeAsync_ServerReportsDecryptedBytes_PublishesThemWithLayerStreamAndDirection_AndCountsOnlyWireBytes()
    {
        var factory = new FakeListenerFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer(
            (connection, context) =>
            {
                context.DecryptedBytes.Received(ExchangeBytesLayer.Decrypted, 4, "ask"u8);
                context.DecryptedBytes.Sent(ExchangeBytesLayer.Decoded, null, "ans"u8);
                return Task.CompletedTask;
            },
            "http");
        var engine = CreateEngine(factory, stream, logs, server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        var events = Drain(subscription);
        var bytes = events.OfType<ExchangeBytesEvent>().ToArray();
        Assert.AreEqual(2, bytes.Length);
        Assert.AreEqual((ExchangeDirection.Received, ExchangeBytesLayer.Decrypted, (long?)4), (bytes[0].Direction, bytes[0].Layer, bytes[0].StreamId));
        CollectionAssert.AreEqual("ask"u8.ToArray(), bytes[0].Bytes.ToArray());
        Assert.AreEqual((ExchangeDirection.Sent, ExchangeBytesLayer.Decoded, (long?)null), (bytes[1].Direction, bytes[1].Layer, bytes[1].StreamId));
        CollectionAssert.AreEqual("ans"u8.ToArray(), bytes[1].Bytes.ToArray());
        var ended = events.OfType<ExchangeEndedEvent>().Single();
        Assert.AreEqual((0L, 0L), (ended.ReceivedByteCount, ended.SentByteCount));

        var notes = logs.LogOf(1).Entries.Select(entry => entry.Text).Where(text => text.Contains("bytes ")).ToArray();
        Assert.AreEqual(2, notes.Length);
        StringAssert.Contains(notes[0], "Decrypted");
        StringAssert.Contains(notes[0], "stream 4");
        StringAssert.Contains(notes[1], "Decoded");
        Assert.IsFalse(notes[1].Contains("stream", StringComparison.Ordinal));
        Assert.AreEqual(0, logs.LogOf(1).Entries.Count(entry => entry.Bytes.Length > 0));
    }

    [TestMethod]
    public async Task ServeAsync_ServerReportsDecryptedBytesWhileNoSubscriptionWantsBytes_PublishesNoneButStillNotesThem()
    {
        var factory = new FakeListenerFactory();
        var stream = new SurlEventStream(new ManualTimeProvider());
        await using var subscription = stream.Subscribe(new SurlEventSubscriptionOptions());
        var logs = new FakeExchangeLogFactory();
        var server = new FakeConnectionProtocolServer(
            (connection, context) =>
            {
                context.DecryptedBytes.Received(ExchangeBytesLayer.Decrypted, 0, "x"u8);
                return Task.CompletedTask;
            },
            "http");
        var engine = CreateEngine(factory, stream, logs, server);
        using var stop = new CancellationTokenSource();
        var serving = engine.ServeAsync([Http], stop.Token);

        factory.ListenerFor(Http).Connect(new InMemoryConnection([]));
        await server.NextExchangeAsync();
        await stop.CancelAsync();
        await serving.WaitAsync(Patience.Timeout);

        Assert.AreEqual(0, Drain(subscription).OfType<ExchangeBytesEvent>().Count());
        Assert.AreEqual(1, logs.LogOf(1).Entries.Count(entry => entry.Text.Contains("stream 0", StringComparison.Ordinal)));
    }
}
