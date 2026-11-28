using System.Text;
using System.Threading.Channels;
using Surl.Cli;
using Surl.Core;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Composition;

/// <summary>
/// A <c>close=graceful</c> drop fault on the datagram side of <c>dns</c> as <c>surl</c> composes it (ADR-0188,
/// amendment 6; BL-1815): a DNS over QUIC connection is closed with <c>CONNECTION_CLOSE</c> <c>NO_ERROR</c>, a
/// plain UDP flow is only silenced. Over <see cref="FakeListenerFactory"/> with no network.
/// </summary>
public sealed partial class SurlServerCompositionTests
{
    // id 0 (RFC 9250 section 4.2.1), RD, one question: big.example.com. IN TXT.
    private static readonly byte[] BigExampleComTxtQuery =
    [
        0, 0, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0,
        3, (byte)'b', (byte)'i', (byte)'g', 7, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e',
        3, (byte)'c', (byte)'o', (byte)'m', 0, 0, 16, 0, 1,
    ];

    [TestMethod]
    public async Task ServeAsync_GracefulDropRuleOnADnsOverQuicFlow_ClosesTheConnectionWithNoErrorAndPublishesGraceful()
    {
        // 12,000 bytes hold the handshake, with room for probe retransmissions on a stalled machine, and end
        // inside the answer of 80 TXT records of about 250 bytes each.
        const int AfterBytes = 12_000;
        var flows = Channel.CreateUnbounded<IDatagramFlow>();
        using var client = new CompositionDnsOverQuicClient();
        await using var serving = await ComposeDnsWithAGracefulDropAsync(flows, AfterBytes, "--self-signed", "--dns-transport", "quic");
        await serving.Stores.SeedZoneAsync("example.com", BigZone(), TestContext.CancellationToken);
        await using var subscription = serving.Events.Subscribe(
            new SurlEventSubscriptionOptions { Filter = surlEvent => surlEvent is FaultAppliedEvent });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);

        flows.Writer.TryWrite(client.Flow);
        await client.QueryAsync(BigExampleComTxtQuery, TestContext.CancellationToken);
        await client.UntilAsync(() => client.Closes.Any(), TestContext.CancellationToken);
        var applied = await NextFaultAsync(subscription);
        await stop.CancelAsync();
        await serve;

        var closes = client.Closes.Select(frame => (frame.IsApplicationError, frame.ErrorCode)).Distinct();
        Assert.AreEqual((false, (ulong)QuicTransportErrorCode.NoError), closes.Single());
        Assert.IsTrue(client.AnsweredByteCount is > 0 and < AfterBytes, $"{client.AnsweredByteCount} bytes of the answer");
        Assert.AreEqual(FaultClose.Graceful, applied.Close);
    }

    [TestMethod]
    public async Task ServeAsync_GracefulDropRuleOnAPlainUdpDnsFlow_SilencesTheFlowAndPublishesReset()
    {
        var flows = Channel.CreateUnbounded<IDatagramFlow>();
        await using var flow = new InMemoryDatagramFlow(BigExampleComTxtQuery, []);
        await using var serving = await ComposeDnsWithAGracefulDropAsync(flows, afterBytes: 10);
        await serving.Stores.SeedZoneAsync("example.com", BigZone(), TestContext.CancellationToken);
        await using var subscription = serving.Events.Subscribe(
            new SurlEventSubscriptionOptions { Filter = surlEvent => surlEvent is FaultAppliedEvent });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);

        flows.Writer.TryWrite(flow);
        var applied = await NextFaultAsync(subscription);
        await stop.CancelAsync();
        await serve;

        Assert.AreEqual(FaultClose.Reset, applied.Close);
        Assert.IsEmpty(flow.SentDatagrams);
    }

    private static string BigZone()
    {
        var zone = new StringBuilder("$TTL 3600\n@ IN SOA ns.example.com. host.example.com. 1 7200 3600 1209600 300\n");
        for (var record = 10; record < 90; record++)
        {
            zone.Append("big IN TXT \"").Append(record).Append('a', 248).Append("\"\n");
        }

        return zone.ToString();
    }

    private async Task<FaultAppliedEvent> NextFaultAsync(SurlEventSubscription subscription)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        patience.CancelAfter(TimeSpan.FromSeconds(30));
        SurlEvent? surlEvent;
        while (!subscription.TryRead(out surlEvent))
        {
            await Task.Delay(1, patience.Token);
        }

        return (FaultAppliedEvent)surlEvent!;
    }

    private async Task<ComposedServing> ComposeDnsWithAGracefulDropAsync(
        Channel<IDatagramFlow> flows, int afterBytes, params string[] options)
    {
        string[] args =
            [.. options, "--allow-anonymous", "--fault", $"drop;after-bytes={afterBytes};close=graceful", "dns://127.0.0.1:5300"];
        var start = await SurlServerComposition.ComposeAsync(
            CommandLineParser.Parse(args, ["dns"]).CommandLine!,
            new StartTestSeams(new FakeListenerFactory { DatagramFlows = flows }).Seams with { AreExtensionsTurnedOn = true },
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        return start.Serving!;
    }
}
