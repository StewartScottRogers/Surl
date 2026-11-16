using System.Text;
using System.Threading.Channels;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    // ADR-0169, BL-1272: an HTTP/3 request's :path reads back from the composition's event stream.
    [TestMethod]
    public async Task ServeAsync_Http3Request_PublishesItsPathInTheDecodedExchangeBytesEvents()
    {
        var events = await GetOverHttp3Async("/reads-back-over-http3");

        var decoded = events
            .OfType<ExchangeBytesEvent>()
            .Where(bytes => bytes.Layer == ExchangeBytesLayer.Decoded && bytes.Direction == ExchangeDirection.Received)
            .Select(bytes => Encoding.UTF8.GetString(bytes.Bytes.Span))
            .ToList();

        Assert.IsTrue(
            decoded.Any(text => text.Contains(":path: /reads-back-over-http3\r\n", StringComparison.Ordinal)),
            string.Join("|", decoded));
    }

    [TestMethod]
    public async Task ServeAsync_Http3Request_StillPublishesTheWireChunksOfTheSameExchange()
    {
        var events = await GetOverHttp3Async("/wire-chunks");

        var bytesEvents = events.OfType<ExchangeBytesEvent>().ToList();
        var decodedExchange = bytesEvents.First(bytes => bytes.Layer == ExchangeBytesLayer.Decoded).ExchangeId;

        Assert.IsTrue(bytesEvents.Any(bytes =>
            bytes.Layer == ExchangeBytesLayer.Wire && bytes.ExchangeId == decodedExchange && bytes.Direction == ExchangeDirection.Received && bytes.Bytes.Length > 0));
        Assert.IsTrue(bytesEvents.Any(bytes =>
            bytes.Layer == ExchangeBytesLayer.Wire && bytes.ExchangeId == decodedExchange && bytes.Direction == ExchangeDirection.Sent && bytes.Bytes.Length > 0));
    }

    // Starts https with HTTP/3, sends one GET over a hand-built QUIC client, and returns the events published
    // by the time the Decoded request headers have been.
    private async Task<List<SurlEvent>> GetOverHttp3Async(string path)
    {
        using var client = new CompositionHttp3QuicClient();
        var flows = Channel.CreateUnbounded<IDatagramFlow>();
        flows.Writer.TryWrite(client.Flow);
        var seams = new StartTestSeams(new FakeListenerFactory { DatagramFlows = flows });
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--self-signed", "--http3", "https://127.0.0.1:0"), seams.Seams, TestContext.CancellationToken);
        await using var serving = start.Serving!;
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions { IncludeBytes = true });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        patience.CancelAfter(TimeSpan.FromSeconds(30));
        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);

        await client.GetAsync(path, patience.Token);
        List<SurlEvent> events = [];
        await client.UntilAsync(
            () =>
            {
                events.AddRange(Drain(subscription));
                return events.OfType<ExchangeBytesEvent>().Any(bytes => bytes.Layer == ExchangeBytesLayer.Decoded)
                    && events.OfType<ExchangeBytesEvent>().Any(bytes => bytes.Layer == ExchangeBytesLayer.Wire && bytes.Direction == ExchangeDirection.Sent);
            },
            patience.Token);
        await stop.CancelAsync();
        await serve;

        return events;
    }
}
