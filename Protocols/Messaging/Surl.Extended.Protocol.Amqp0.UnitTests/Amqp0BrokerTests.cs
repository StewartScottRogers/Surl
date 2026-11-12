using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// The connection class (BL-505; ADR-0112 decisions 2, 3, 4 and 7), every byte written by hand
/// from AMQP 0-9-1's section 4.2 and the XML's <c>connection</c> class.
/// </summary>
[TestClass]
public sealed class Amqp0BrokerTests
{
    private static readonly byte[] PlainStartOk = StartOk("PLAIN", PlainResponse("alice", "secret"));
    private static readonly byte[] DefaultTuneOk = TuneOk(0, 0, 0);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullPolicy_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp0Broker(null!, NewBus()));

    [TestMethod]
    public void Constructor_NullBus_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy(), null!));

    private static MessageBus.MessageBus NewBus() => new(new ManualTimeProvider());

    [TestMethod]
    public void Schemes_AreAmqpAndAmqps() =>
        CollectionAssert.AreEqual(new[] { "amqp", "amqps" }, new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy(), NewBus()).Schemes.ToArray());

    [TestMethod]
    public void ServeAsync_NullArguments_Throw()
    {
        var broker = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy(), NewBus());

        Assert.ThrowsExactly<ArgumentNullException>(() => broker.ServeAsync(null!, Context(new ManualTimeProvider())));
        Assert.ThrowsExactly<ArgumentNullException>(() => broker.ServeAsync(new InMemoryConnection([]), null!));
    }

    [TestMethod]
    public async Task ServeAsync_PlainLogin_OpensTheConnectionAndAnswersCloseWithCloseOk()
    {
        var policy = new UnitTestAmqp0AuthenticationPolicy("PLAIN");

        var (written, log) = await ServeAsync(policy, ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/"), Close());

        CollectionAssert.AreEqual(Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk(), CloseOk()), written);
        Assert.AreEqual("PLAIN", policy.Starts.Single().Mechanism);
        CollectionAssert.Contains(log.Notes.ToList(), "The client sent connection.close; answered close-ok.");
    }

    [TestMethod]
    public async Task ServeAsync_AmqplainLogin_GoesToThePolicyAsPlainAndOpens()
    {
        var policy = new UnitTestAmqp0AuthenticationPolicy("PLAIN");

        var (written, _) = await ServeAsync(
            policy, ProtocolHeader, StartOk("AMQPLAIN", AmqplainResponse("alice", "secret")), DefaultTuneOk, Open("/"), Close());

        CollectionAssert.AreEqual(Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk(), CloseOk()), written);
        var start = policy.Starts.Single();
        Assert.AreEqual("PLAIN", start.Mechanism);
        CollectionAssert.AreEqual(PlainResponse("alice", "secret"), start.InitialResponse!.Value.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_WrongPassword_IsClosedWith403NamingStartOk()
    {
        var (written, _) = await ServeAsync(
            new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, StartOk("PLAIN", PlainResponse("alice", "wrong")), CloseOk());

        CollectionAssert.AreEqual(Concat(Start("PLAIN AMQPLAIN"), BrokerClose(403, "ACCESS_REFUSED - login refused", 10, 11)), written);
    }

    [TestMethod]
    public async Task ServeAsync_MalformedAmqplain_IsClosedWith403WithoutAskingThePolicy()
    {
        var policy = new UnitTestAmqp0AuthenticationPolicy("PLAIN");

        var (written, _) = await ServeAsync(policy, ProtocolHeader, StartOk("AMQPLAIN", [.. ShortString("LOGIN"), (byte)'S']), CloseOk());

        CollectionAssert.AreEqual(Concat(Start("PLAIN AMQPLAIN"), BrokerClose(403, "ACCESS_REFUSED - login refused", 10, 11)), written);
        Assert.IsEmpty(policy.Starts);
    }

    [TestMethod]
    public async Task ServeAsync_MechanismNotOffered_ClosesWithNoFurtherBytes()
    {
        var (written, _) = await ServeAsync(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, StartOk("CRAM-MD5", []));

        CollectionAssert.AreEqual(Start("PLAIN AMQPLAIN"), written);
    }

    [TestMethod]
    public async Task ServeAsync_OffersAnonymous_OffersItLastAndAcceptsItAtOnce()
    {
        var policy = new UnitTestAmqp0AuthenticationPolicy("SCRAM-SHA-256");
        var broker = new Amqp0Broker(policy, NewBus(), new Amqp0BrokerOptions { OffersAnonymous = true });

        var (written, _) = await ServeAsync(broker, ProtocolHeader, StartOk("ANONYMOUS", []), DefaultTuneOk, Open("/"), Close());

        CollectionAssert.AreEqual(Concat(Start("SCRAM-SHA-256 ANONYMOUS"), Tune(), OpenOk(), CloseOk()), written);
        Assert.IsEmpty(policy.Starts);
    }

    [TestMethod]
    public async Task ServeAsync_PolicyOffersAnonymousItself_OffersItOnce()
    {
        var broker = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("ANONYMOUS"), NewBus(), new Amqp0BrokerOptions { OffersAnonymous = true });

        var (written, _) = await ServeAsync(broker, ProtocolHeader);

        CollectionAssert.AreEqual(Start("ANONYMOUS"), written);
    }

    [TestMethod]
    public async Task ServeAsync_ChallengeMechanism_RunsSecureAndSecureOkUntilAccepted()
    {
        var policy = new UnitTestAmqp0AuthenticationPolicy("CRAM-MD5")
        {
            ScriptedSteps =
            [
                UnitTestAmqp0AuthenticationPolicy.Challenge("<1@surl>"),
                new(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, "alice", null),
            ],
        };

        var (written, _) = await ServeAsync(policy, ProtocolHeader, StartOk("CRAM-MD5", []), SecureOk("alice 0123"), DefaultTuneOk, Open("/"), Close());

        CollectionAssert.AreEqual(Concat(Start("CRAM-MD5"), Method(0, 10, 20, LongString("<1@surl>")), Tune(), OpenOk(), CloseOk()), written);
        Assert.IsNull(policy.Starts.Single().InitialResponse);
        CollectionAssert.AreEqual(new[] { "alice 0123" }, policy.ScriptedResponses);
    }

    [TestMethod]
    public async Task ServeAsync_RefusedAfterSecureOk_IsClosedWith403NamingSecureOk()
    {
        var policy = new UnitTestAmqp0AuthenticationPolicy("CRAM-MD5")
        {
            ScriptedSteps =
            [
                UnitTestAmqp0AuthenticationPolicy.Challenge("c"),
                new(SaslLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, new CheckedLogin("CRAM-MD5", "alice", false), "wrong"),
            ],
        };

        var (written, log) = await ServeAsync(policy, ProtocolHeader, StartOk("CRAM-MD5", [0x61]), SecureOk("x"), Close());

        CollectionAssert.AreEqual(
            Concat(Start("CRAM-MD5"), Method(0, 10, 20, LongString("c")), BrokerClose(403, "ACCESS_REFUSED - login refused", 10, 21), CloseOk()),
            written);
        CollectionAssert.Contains(log.Notes.ToList(), "wrong");
    }

    [TestMethod]
    public async Task ServeAsync_ClientGoneDuringSecure_ClosesWithNoFurtherBytes()
    {
        var policy = new UnitTestAmqp0AuthenticationPolicy("CRAM-MD5") { ScriptedSteps = [UnitTestAmqp0AuthenticationPolicy.Challenge("c")] };

        var (written, _) = await ServeAsync(policy, ProtocolHeader, StartOk("CRAM-MD5", []));

        CollectionAssert.AreEqual(Concat(Start("CRAM-MD5"), Method(0, 10, 20, LongString("c"))), written);
    }

    [TestMethod]
    public async Task ServeAsync_UnknownVirtualHost_IsClosedWith530NamingOpen()
    {
        var (written, _) = await ServeAsync(
            new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("tenant-a"), CloseOk());

        CollectionAssert.AreEqual(Concat(Start("PLAIN AMQPLAIN"), Tune(), BrokerClose(530, "NOT_ALLOWED - unknown virtual host", 10, 40)), written);
    }

    [TestMethod]
    [DataRow(257, 0L, DisplayName = "channel-max above 256")]
    [DataRow(0, 131073L, DisplayName = "frame-max above 131072")]
    [DataRow(0, 4095L, DisplayName = "frame-max below 4096")]
    public async Task ServeAsync_TuneOkAboveTheMaximums_ClosesWithNoFurtherBytes(int channelMax, long frameMax)
    {
        var (written, log) = await ServeAsync(
            new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, PlainStartOk, TuneOk(channelMax, frameMax, 0), Open("/"));

        CollectionAssert.AreEqual(Concat(Start("PLAIN AMQPLAIN"), Tune()), written);
        Assert.IsTrue(log.Notes.Any(note => note.StartsWith("connection.tune-ok asked for", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_TuneOkFrameMax_BoundsTheFramesRead()
    {
        byte[] large = Method(0, 10, 50, new byte[4100]);

        var (written, _) = await ServeAsync(
            new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, PlainStartOk, TuneOk(256, 4096, 0), Open("/"), large, CloseOk());

        CollectionAssert.AreEqual(Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk(), BrokerClose(501, "FRAME_ERROR - malformed frame", 0, 0)), written);
    }

    [TestMethod]
    public async Task ServeAsync_HeartbeatNegotiated_IsSentAfterTheIntervalWithNothingWritten()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([ProtocolHeader, PlainStartOk, TuneOk(0, 0, 10), Open("/")], peerHalfClosesWhenExhausted: false);
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, Context(clock));
        byte[] opened = Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk());
        await WaitUntilAsync(() => connection.WrittenBytes.Length == opened.Length && clock.PendingTimerCount == 2);

        clock.Advance(TimeSpan.FromSeconds(9));
        await Task.Yield();
        var beforeInterval = connection.WrittenBytes;
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitUntilAsync(() => connection.WrittenBytes.Length > opened.Length);

        CollectionAssert.AreEqual(opened, beforeInterval);
        CollectionAssert.AreEqual(Concat(opened, Heartbeat), connection.WrittenBytes);
        connection.Abort();
        await Assert.ThrowsExactlyAsync<IOException>(() => serving);
    }

    [TestMethod]
    public async Task ServeAsync_PeerSilentForTwoIntervals_IsClosedWithoutConnectionClose()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([ProtocolHeader, PlainStartOk, TuneOk(0, 0, 10), Heartbeat, Open("/")], peerHalfClosesWhenExhausted: false);
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, Context(clock, log));
        byte[] opened = Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk());
        await WaitUntilAsync(() => connection.WrittenBytes.Length == opened.Length && clock.PendingTimerCount == 2);

        clock.Advance(TimeSpan.FromSeconds(20));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        var afterOpen = connection.WrittenBytes[opened.Length..];
        Assert.AreEqual(0, afterOpen.Length % Heartbeat.Length);
        CollectionAssert.AreEqual(Enumerable.Repeat(Heartbeat, afterOpen.Length / Heartbeat.Length).SelectMany(frame => frame).ToArray(), afterOpen);
        CollectionAssert.Contains(log.Notes.ToList(), "Nothing arrived for two heartbeat intervals; closed without connection.close.");
    }

    [TestMethod]
    public async Task ServeAsync_MethodOnUnopenedChannel_IsClosedWith504()
    {
        var queueDeclare = Method(1, 50, 10, [.. Short(0), .. ShortString("orders"), 0, 0, 0, 0, 0]);

        var (written, _) = await ServeAsync(
            new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/"), Heartbeat, queueDeclare, CloseOk());

        CollectionAssert.AreEqual(
            Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk(), BrokerClose(504, "CHANNEL_ERROR - channel not open", 50, 10)), written);
    }

    [TestMethod]
    [DataRow((byte)3, DisplayName = "a content body frame")]
    [DataRow((byte)1, DisplayName = "a method frame too short for its IDs")]
    public async Task ServeAsync_UndecodableFrameOnUnopenedChannel_IsClosedWith504NamingNoMethod(byte frameType)
    {
        var (written, _) = await ServeAsync(
            new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/"), Frame(frameType, 1, [0x61]), CloseOk());

        CollectionAssert.AreEqual(
            Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk(), BrokerClose(504, "CHANNEL_ERROR - channel not open", 0, 0)), written);
    }

    [TestMethod]
    public async Task ServeAsync_ClientGoneBeforeTuneOk_ClosesWithNoFurtherBytes()
    {
        var (written, _) = await ServeAsync(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, PlainStartOk);

        CollectionAssert.AreEqual(Concat(Start("PLAIN AMQPLAIN"), Tune()), written);
    }

    [TestMethod]
    public async Task ServeAsync_PeerSilentForTwoIntervalsBeforeOpen_ClosesWithNoFurtherBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([ProtocolHeader, PlainStartOk, TuneOk(0, 0, 10)], peerHalfClosesWhenExhausted: false);
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, Context(clock, log));
        await WaitUntilAsync(() => clock.PendingTimerCount == 3);

        clock.Advance(TimeSpan.FromSeconds(20));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsFalse(connection.WrittenBytes.AsSpan().IndexOf(OpenOk()) >= 0);
        CollectionAssert.Contains(log.Notes.ToList(), "No method frame arrived during the negotiation (silent); closed with no reply.");
    }

    [TestMethod]
    public async Task ServeAsync_HeartbeatWriteStalledWhenTheConnectionEnds_IsCancelledAndAwaited()
    {
        var clock = new ManualTimeProvider();
        var inner = new InMemoryConnection([ProtocolHeader, PlainStartOk, TuneOk(0, 0, 10), Open("/")], peerHalfClosesWhenExhausted: false);
        var connection = new UnitTestHeartbeatStallingConnection(inner);
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, Context(clock));
        await WaitUntilAsync(() => inner.WrittenBytes.Length == Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk()).Length && clock.PendingTimerCount == 2);
        clock.Advance(TimeSpan.FromSeconds(10));
        await WaitUntilAsync(() => connection.HeartbeatStalled);

        clock.Advance(TimeSpan.FromSeconds(10));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsTrue(connection.HeartbeatCancelled);
    }

    [TestMethod]
    public async Task ServeAsync_ContentFrameOnChannelZero_IsClosedWith505()
    {
        var (written, _) = await ServeAsync(
            new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/"), Frame(3, 0, [0x61]), CloseOk());

        CollectionAssert.AreEqual(
            Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk(), BrokerClose(505, "UNEXPECTED_FRAME - content frame on channel 0", 0, 0)), written);
    }

    [TestMethod]
    public async Task ServeAsync_ShortMethodOnChannelZero_IsClosedWith502()
    {
        var (written, _) = await ServeAsync(
            new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/"), Frame(1, 0, [0x00]), CloseOk());

        CollectionAssert.AreEqual(
            Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk(), BrokerClose(502, "SYNTAX_ERROR - malformed method", 0, 0)), written);
    }

    [TestMethod]
    [DataRow(10, 40, DisplayName = "a second connection.open")]
    [DataRow(20, 10, DisplayName = "channel.open on channel 0")]
    public async Task ServeAsync_OtherMethodOnChannelZero_IsClosedWith503(int classId, int methodId)
    {
        var (written, _) = await ServeAsync(
            new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/"), Method(0, classId, methodId), CloseOk());

        CollectionAssert.AreEqual(
            Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk(), BrokerClose(503, "COMMAND_INVALID - method not valid on channel 0", classId, methodId)), written);
    }

    [TestMethod]
    public async Task ServeAsync_AfterBrokerClose_DiscardsFramesAndAnswersTheClientsCloseWithCloseOk()
    {
        var (written, _) = await ServeAsync(
            new UnitTestAmqp0AuthenticationPolicy("PLAIN"),
            ProtocolHeader,
            PlainStartOk,
            DefaultTuneOk,
            Open("tenant-a"),
            Heartbeat,
            Method(1, 20, 10),
            Frame(1, 0, [0x00]),
            Method(0, 20, 10),
            Close(),
            Close());

        CollectionAssert.AreEqual(
            Concat(Start("PLAIN AMQPLAIN"), Tune(), BrokerClose(530, "NOT_ALLOWED - unknown virtual host", 10, 40), CloseOk()), written);
    }

    [TestMethod]
    public async Task ServeAsync_NoCloseOkWithinOneSecond_EndsTheConnection()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("x")], peerHalfClosesWhenExhausted: false);
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, Context(clock, log));
        await WaitUntilAsync(() => log.Notes.Any(note => note.StartsWith("Closed the connection with 530", StringComparison.Ordinal)) && clock.PendingTimerCount == 2);

        clock.Advance(TimeSpan.FromSeconds(1));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        CollectionAssert.Contains(log.Notes.ToList(), "No connection.close-ok arrived within one second.");
    }

    [TestMethod]
    public async Task ServeAsync_UnaskedCloseOk_EndsTheConnection()
    {
        var (written, log) = await ServeAsync(
            new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/"), CloseOk(), Close());

        CollectionAssert.AreEqual(Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk()), written);
        CollectionAssert.Contains(log.Notes.ToList(), "The client sent connection.close-ok unasked; closed.");
    }

    [TestMethod]
    public async Task ServeAsync_ClientGoneAfterOpen_EndsWithNoFurtherBytes()
    {
        var (written, log) = await ServeAsync(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/"));

        CollectionAssert.AreEqual(Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk()), written);
        CollectionAssert.Contains(log.Notes.ToList(), "The client closed the connection without connection.close (ConnectionClosed).");
    }

    [TestMethod]
    public async Task ServeAsync_OtherProtocolHeader_IsAnsweredWithTheSupportedOne()
    {
        var (written, _) = await ServeAsync(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), [0x41, 0x4D, 0x51, 0x50, 0x00, 0x01, 0x00, 0x00]);

        CollectionAssert.AreEqual(ProtocolHeader, written);
    }

    public static IEnumerable<object[]> NegotiationFaults =>
    [
        [new[] { ProtocolHeader, TuneOk(0, 0, 0) }],
        [new[] { ProtocolHeader, Method(0, 20, 11) }],
        [new[] { ProtocolHeader, Method(0, 10, 11, 0x00) }],
        [new[] { ProtocolHeader, Frame(1, 0, [0x00]) }],
        [new[] { ProtocolHeader, Frame(3, 0, [0x61]) }],
        [new[] { ProtocolHeader, Method(1, 10, 11) }],
        [new[] { ProtocolHeader, new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 } }],
    ];

    [TestMethod]
    [DynamicData(nameof(NegotiationFaults))]
    public async Task ServeAsync_FaultBeforeStartOk_ClosesWithNoFurtherBytes(byte[][] chunks)
    {
        var (written, _) = await ServeAsync(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), chunks);

        CollectionAssert.AreEqual(Start("PLAIN AMQPLAIN"), written);
    }

    [TestMethod]
    public async Task ServeAsync_HeartbeatDuringNegotiation_IsSkipped()
    {
        var (written, _) = await ServeAsync(
            new UnitTestAmqp0AuthenticationPolicy("PLAIN"), ProtocolHeader, PlainStartOk, DefaultTuneOk, Heartbeat, Open("/"), Close());

        CollectionAssert.AreEqual(Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk(), CloseOk()), written);
    }

    [TestMethod]
    public async Task ServeAsync_NoOpenWithinTheHeadTimeout_ClosesWithNoFurtherBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([ProtocolHeader], peerHalfClosesWhenExhausted: false);
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, Context(clock, log));
        await WaitUntilAsync(() => connection.WrittenBytes.Length > 0 && clock.PendingTimerCount == 1);

        clock.Advance(TimeSpan.FromSeconds(30));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        CollectionAssert.AreEqual(Start("PLAIN AMQPLAIN"), connection.WrittenBytes);
        CollectionAssert.Contains(log.Notes.ToList(), "The connection was not open within the head timeout; closed with no reply.");
    }

    [TestMethod]
    public async Task ServeAsync_PartHeaderWithinTheHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var connection = new InMemoryConnection([ProtocolHeader.AsMemory(0, 5)], peerHalfClosesWhenExhausted: false);
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, Context(clock));
        await WaitUntilAsync(() => clock.PendingTimerCount == 1);

        clock.Advance(TimeSpan.FromSeconds(30));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new InMemoryConnection([ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/")], peerHalfClosesWhenExhausted: false);
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, Context(new ManualTimeProvider(), cancellation: cancellation.Token));
        await WaitUntilAsync(() => connection.WrittenBytes.Length == Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk()).Length);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledDuringNegotiation_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new InMemoryConnection([ProtocolHeader], peerHalfClosesWhenExhausted: false);
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, Context(new ManualTimeProvider(), cancellation: cancellation.Token));
        await WaitUntilAsync(() => connection.WrittenBytes.Length > 0);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledWhileAwaitingCloseOk_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/"), Method(0, 20, 10)], peerHalfClosesWhenExhausted: false);
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, Context(new ManualTimeProvider(), log, cancellation.Token));
        await WaitUntilAsync(() => log.Notes.Any(note => note.StartsWith("Closed the connection with 503", StringComparison.Ordinal)));

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    private const string ForcedCloseText = "CONNECTION_FORCED - idle timeout, maximum time or shutdown";

    [TestMethod]
    public async Task ServeAsync_OpenConnectionCancelledForALimit_IsClosedWith320ThenThrows()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new InMemoryConnection([ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/")], peerHalfClosesWhenExhausted: false);
        byte[] opened = Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk());
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, Context(new ManualTimeProvider(), null, cancellation.Token));
        await WaitUntilAsync(() => connection.WrittenBytes.Length == opened.Length);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        CollectionAssert.AreEqual(Concat(opened, BrokerClose(320, ForcedCloseText, 0, 0)), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_OpenConnectionCancelledAtShutdown_IsClosedWith320ThenThrows()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new InMemoryConnection([ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/")], peerHalfClosesWhenExhausted: false);
        byte[] opened = Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk());
        var context = Context(new ManualTimeProvider(), null, cancellation.Token) with { ShutdownToken = cancellation.Token };
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, context);
        await WaitUntilAsync(() => connection.WrittenBytes.Length == opened.Length);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        CollectionAssert.AreEqual(Concat(opened, BrokerClose(320, ForcedCloseText, 0, 0)), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_FarewellStuckPastItsWriteDeadline_IsGivenUpAndThrows()
    {
        using var cancellation = new CancellationTokenSource();
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        byte[] opened = Concat(Start("PLAIN AMQPLAIN"), Tune(), OpenOk());
        var inner = new InMemoryConnection([ProtocolHeader, PlainStartOk, DefaultTuneOk, Open("/")], peerHalfClosesWhenExhausted: false);
        var connection = new UnitTestOpenOkThenStallingConnection(inner, opened.Length);
        var serving = new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy("PLAIN"), NewBus()).ServeAsync(connection, Context(clock, log, cancellation.Token));
        await WaitUntilAsync(() => inner.WrittenBytes.Length == opened.Length);

        await cancellation.CancelAsync();
        await WaitUntilAsync(() => connection.Stalled);
        clock.Advance(Amqp0BrokerConnection.FarewellWriteDeadline);

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        CollectionAssert.AreEqual(opened, inner.WrittenBytes);
        CollectionAssert.Contains(log.Notes.ToList(), "The connection.close 320 was not written within its 1-second write deadline.");
    }

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(UnitTestAmqp0AuthenticationPolicy policy, params byte[][] chunks) =>
        await ServeAsync(new Amqp0Broker(policy, NewBus()), chunks);

    private async Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(Amqp0Broker broker, params byte[][] chunks)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(chunks.Select(chunk => (ReadOnlyMemory<byte>)chunk));

        await broker.ServeAsync(connection, Context(new ManualTimeProvider(), log, TestContext.CancellationToken));

        return (connection.WrittenBytes, log);
    }

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The broker did not reach the expected state in time.");
            }

            await Task.Delay(1, TestContext.CancellationToken);
        }
    }

    private static ExchangeContext Context(TimeProvider clock, IExchangeLog? log = null, CancellationToken cancellation = default) => new(
        1,
        new ListenUrl("amqp", "127.0.0.1", 5672).WithBoundPort(5672),
        new IPEndPoint(IPAddress.Loopback, 5672),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log ?? new RecordingExchangeLog(),
        clock,
        cancellation);
}
