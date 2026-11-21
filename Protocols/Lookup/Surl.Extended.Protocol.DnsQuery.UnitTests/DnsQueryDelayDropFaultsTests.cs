using System.Net;
using System.Text;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using Surl.Quic;
using static Surl.Extended.Protocol.DnsQuery.DnsTestQueries;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The command-stage delay and drop faults (ADR-0124 decision 4; ADR-0141; BL-1183): a
/// <see cref="FaultKind.Delay"/> rule holds a query's answer for its <c>ms</c> on the exchange's clock
/// and the query is then answered from the zones, and a <see cref="FaultKind.Drop"/> rule leaves the
/// query unanswered - no datagram over UDP, the connection aborted over TCP, a reset stream over DNS
/// over QUIC, a dropped exchange over DNS over HTTPS - each publishing one <see cref="FaultAppliedEvent"/>.
/// </summary>
[TestClass]
public sealed class DnsQueryDelayDropFaultsTests
{
    private const ushort TypeA = 1;

    public TestContext TestContext { get; set; } = null!;

    private static FaultRule DelayRule() =>
        new("d1", FaultKind.Delay) { Scheme = "dns", Command = "A", Path = "web.example.test", Stage = FaultStage.Command, DelayMilliseconds = 500 };

    private static FaultRule DropRule(string path = "web.example.test") =>
        new("x1", FaultKind.Drop) { Scheme = "dns", Command = "A", Path = path, Stage = FaultStage.Command };

    [TestMethod]
    public async Task ServeAsync_UdpQueryUnderADelayRule_IsAnsweredOnlyAfterTheDelay()
    {
        var time = new ManualTimeProvider();
        var events = new RecordingSurlEventPublisher();
        await using var flow = new InMemoryDatagramFlow(Query("web.example.test", TypeA));
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new DnsUdpProtocolServer(DnsTestZones.LoadExample()).ServeAsync(flow, Context(new ScriptedFaultPlan(DelayRule()), events, time, exchange.Token));

        await UntilAsync(() => time.PendingTimerCount == 1);
        Assert.IsEmpty(flow.SentDatagrams);
        time.Advance(TimeSpan.FromMilliseconds(500));
        await UntilAsync(() => flow.SentDatagrams.Count == 1);
        await exchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);

        Assert.AreEqual(IPAddress.Parse("192.0.2.80"), ((DnsAddressData)ReadWhole(flow.SentDatagrams.Single()).Answers.Single().Data).Address);
        Assert.AreEqual(new FaultAppliedEvent("d1", FaultKind.Delay, FaultStage.Command, 1, "web.example.test"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeAsync_TcpQueryUnderADelayRule_IsAnsweredOnlyAfterTheDelay()
    {
        var time = new ManualTimeProvider();
        var events = new RecordingSurlEventPublisher();
        var query = Query("web.example.test", TypeA);
        // The client stays connected: a script that half-closes would end the hold at once (ADR-0141 amendment 2).
        var connection = new InMemoryConnection([(ReadOnlyMemory<byte>)(byte[])[0, (byte)query.Length, .. query]], peerHalfClosesWhenExhausted: false);
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new DnsTcpProtocolServer(DnsTestZones.LoadExample()).ServeAsync(connection, Context(new ScriptedFaultPlan(DelayRule()), events, time, exchange.Token));

        await UntilAsync(() => events.Events.Count == 1 && time.PendingTimerCount == 1);
        Assert.IsEmpty(connection.WrittenBytes);
        time.Advance(TimeSpan.FromMilliseconds(500));
        await UntilAsync(() => connection.WrittenBytes.Length > 0);
        await exchange.CancelAsync();
        await serving;

        Assert.AreEqual(DnsResponseCode.NoError, ReadWhole(connection.WrittenBytes[2..]).Header.ResponseCode);
        Assert.AreEqual(new FaultAppliedEvent("d1", FaultKind.Delay, FaultStage.Command, 1, "web.example.test"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeAsync_UdpQueryUnderADropRule_GetsNoReply()
    {
        var events = new RecordingSurlEventPublisher();
        await using var flow = new InMemoryDatagramFlow(Query("web.example.test", TypeA));
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new DnsUdpProtocolServer(DnsTestZones.LoadExample()).ServeAsync(flow, Context(new ScriptedFaultPlan(DropRule()), events, new ManualTimeProvider(), exchange.Token));

        await UntilAsync(() => events.Events.Count == 1);
        await exchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);

        Assert.IsEmpty(flow.SentDatagrams);
        Assert.AreEqual(new FaultAppliedEvent("x1", FaultKind.Drop, FaultStage.Command, 1, "web.example.test"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeAsync_TcpQueryUnderADropRule_AbortsTheConnectionWithNothingSent()
    {
        var events = new RecordingSurlEventPublisher();
        var query = Query("web.example.test", TypeA);
        var connection = new InMemoryConnection([(ReadOnlyMemory<byte>)(byte[])[0, (byte)query.Length, .. query]]);

        await new DnsTcpProtocolServer(DnsTestZones.LoadExample()).ServeAsync(connection, Context(new ScriptedFaultPlan(DropRule()), events, new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.IsTrue(connection.Aborted);
        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual(new FaultAppliedEvent("x1", FaultKind.Drop, FaultStage.Command, 1, "web.example.test"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeAsync_UdpQueryUnderADropRuleForAnotherName_IsAnswered()
    {
        var events = new RecordingSurlEventPublisher();
        await using var flow = new InMemoryDatagramFlow(Query("web.example.test", TypeA));
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = new DnsUdpProtocolServer(DnsTestZones.LoadExample()).ServeAsync(flow, Context(new ScriptedFaultPlan(DropRule("mail.example.test")), events, new ManualTimeProvider(), exchange.Token));

        await UntilAsync(() => flow.SentDatagrams.Count == 1);
        await exchange.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);

        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task ServeAsync_QuicQueryUnderADropRule_ResetsItsStreamUnanswered()
    {
        var events = new RecordingSurlEventPublisher();
        var context = DnsOverQuicConnectionServerTests.Context(new RecordingExchangeLog()) with { Faults = new ScriptedFaultPlan(DropRule()), Events = events };
        using var client = new DnsOverQuicTestClient();
        client.Serve(new DnsOverQuicConnectionServer(new DnsQueryAnswerer(DnsTestZones.LoadExample()), context), TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);

        client.SendQuery(0, Query("web.example.test", TypeA, id: 0));
        await client.UntilAsync(() => client.OneRttFrames<QuicResetStreamFrame>().Any(frame => frame.StreamId == 0), TestContext.CancellationToken);

        Assert.IsEmpty(client.StreamSent(0).Data);
        Assert.AreEqual((ulong)DnsOverQuicErrorCode.RequestCancelled, client.OneRttFrames<QuicResetStreamFrame>().Single().ApplicationErrorCode);
        Assert.IsNull(client.CloseCode);
        Assert.AreEqual(FaultKind.Drop, events.Events.OfType<FaultAppliedEvent>().Single().Kind);
    }

    [TestMethod]
    public async Task AnswerAsync_HttpsQueryUnderADropRule_DropsTheExchangeWithNothingSent()
    {
        var events = new RecordingSurlEventPublisher();
        var context = Context(new ScriptedFaultPlan(DropRule()), events, new ManualTimeProvider(), TestContext.CancellationToken);
        var parameter = Convert.ToBase64String(Query("web.example.test", TypeA)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var head = $"GET {DnsOverHttpsHandler.Path}?dns={parameter} HTTP/1.1\r\nHost: dns.example\r\nConnection: close\r\n\r\n";
        var connection = new InMemoryConnection([(ReadOnlyMemory<byte>)Encoding.ASCII.GetBytes(head)]);

        await new Http1ConnectionServer(new DnsOverHttpsHandler(DnsTestZones.LoadExample(), context), context).ServeAsync(connection);

        Assert.IsTrue(connection.Aborted);
        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual(FaultKind.Drop, events.Events.OfType<FaultAppliedEvent>().Single().Kind);
    }

    private async Task UntilAsync(Func<bool> condition)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        patience.CancelAfter(TimeSpan.FromSeconds(30));
        while (!condition())
        {
            await Task.Delay(1, patience.Token);
        }
    }

    private static ExchangeContext Context(IFaultPlan faults, ISurlEventPublisher events, TimeProvider time, CancellationToken cancellationToken) => new ExchangeContext(
        1,
        new ListenUrl("dns", "127.0.0.1", 53).WithBoundPort(53),
        new IPEndPoint(IPAddress.Loopback, 53),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        time,
        cancellationToken) with
    { Faults = faults, Events = events };
}
