using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// The whole container (ADR-0107 decisions 1 to 7): headers and SASL, then the connection, under the
/// head timeout and the engine's limits. The headers and the <c>PLAIN</c> exchange are written by hand
/// from the specification; the AMQP frames are built with the library's codec, which BL-510's tests pin.
/// </summary>
[TestClass]
public sealed class Amqp1ContainerTests
{
    private const string SaslHeader = "414D515003010000";
    private const string AmqpHeader = "414D515000010000";

    // sasl-mechanisms offering PLAIN (Part 5 section 5.3.3.1).
    private const string MechanismsPlain = "0000001502010000" + "005340C00801A305504C41494E";

    // sasl-init PLAIN with the RFC 4616 message "\0alice\0secret".
    private const string InitPlainAliceSecret = "0000002402010000" + "005341C01702A305504C41494EA00D00616C69636500736563726574";

    private const string OutcomeOk = "0000001002010000" + "005344C0030150" + "00";

    // open (Part 2 section 2.7.1) with container-id "client" and every other field left out.
    private const string ClientOpenHex = "0000001602000000" + "005310C00901A106636C69656E74";

    private const string Negotiated = SaslHeader + MechanismsPlain + OutcomeOk + AmqpHeader;

    private const string ContainerId = "surl-00000000000000000000000000000001";

    private static readonly TimeSpan HeadTimeout = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_SaslLoginThenOpenBeginAndAttach_AreServedOnOneConnectionForTheAccount()
    {
        var nodes = new UnitTestAmqp1NodeDirectory();
        var attach = new Amqp1Attach("sender-link", 0, IsReceiver: false)
        {
            Target = new Amqp1Described(Amqp1Descriptor.Target, new List<object?> { "orders" }),
        };
        var connection = new InMemoryConnection([Bytes(SaslHeader + InitPlainAliceSecret + AmqpHeader + ClientOpenHex), Frame(new Amqp1Begin(0, 100, 100)), Frame(attach)]);

        await Container(nodes).ServeAsync(connection, Context(new ManualTimeProvider(), new RecordingExchangeLog(), TestContext.CancellationToken));

        var written = connection.WrittenBytes;
        Assert.AreEqual(Negotiated, Convert.ToHexString(written[..(Negotiated.Length / 2)]));
        var frames = await ReadFramesAsync(written[(Negotiated.Length / 2)..]);
        Assert.AreEqual(ContainerId, ((Amqp1Open)frames[0]).ContainerId);
        Assert.IsInstanceOfType<Amqp1Begin>(frames[1]);
        Assert.AreEqual("sender-link", ((Amqp1Attach)frames[2]).Name);
        Assert.AreEqual(new Amqp1NodeRequest("/queue/orders", false, "alice"), nodes.Requests.Single());
    }

    [TestMethod]
    public async Task ServeAsync_HeadTimeoutLongerThanATimerTakes_IsNoLimit()
    {
        var connection = new InMemoryConnection([Bytes(SaslHeader + InitPlainAliceSecret + AmqpHeader + ClientOpenHex)]);
        var context = Context(TimeProvider.System, new RecordingExchangeLog(), TestContext.CancellationToken) with
        {
            Limits = ExchangeLimits.Default with { HeadTimeout = TimeSpan.FromDays(100) },
        };

        await Container(new UnitTestAmqp1NodeDirectory()).ServeAsync(connection, context);

        Assert.IsInstanceOfType<Amqp1Open>((await ReadFramesAsync(connection.WrittenBytes[(Negotiated.Length / 2)..])).Single());
    }

    [TestMethod]
    public async Task ServeAsync_NegotiationRefused_ServesNoConnection()
    {
        var connection = new InMemoryConnection([Bytes(AmqpHeader + ClientOpenHex)]);

        await Container(new UnitTestAmqp1NodeDirectory()).ServeAsync(connection, Context(new ManualTimeProvider(), new RecordingExchangeLog(), TestContext.CancellationToken));

        Assert.AreEqual(SaslHeader, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsTrue(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_NoOpenWithinTheHeadTimeoutDuringSasl_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Bytes(SaslHeader)], peerHalfClosesWhenExhausted: false);
        var serving = Container(new UnitTestAmqp1NodeDirectory()).ServeAsync(connection, Context(clock, log, TestContext.CancellationToken));
        await WaitUntilAsync(() => connection.WrittenBytes.Length == (SaslHeader + MechanismsPlain).Length / 2);

        clock.Advance(HeadTimeout);
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(SaslHeader + MechanismsPlain, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.Contains(log.Notes.ToList(), "No open arrived within the head timeout; closed with no bytes.");
    }

    [TestMethod]
    public async Task ServeAsync_NoOpenWithinTheHeadTimeoutAfterSasl_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Bytes(SaslHeader + InitPlainAliceSecret + AmqpHeader)], peerHalfClosesWhenExhausted: false);
        var serving = Container(new UnitTestAmqp1NodeDirectory()).ServeAsync(connection, Context(clock, log, TestContext.CancellationToken));
        await WaitUntilAsync(() => connection.WrittenBytes.Length == Negotiated.Length / 2);

        clock.Advance(HeadTimeout - TimeSpan.FromTicks(1));
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(Negotiated, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
        CollectionAssert.Contains(log.Notes.ToList(), "No open arrived within the head timeout; closed with no bytes.");
    }

    [TestMethod]
    public async Task ServeAsync_CancelledForMaxTimeAfterOpen_ClosesWithResourceLimitExceeded()
    {
        using var cancellation = new CancellationTokenSource();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Bytes(SaslHeader + InitPlainAliceSecret + AmqpHeader + ClientOpenHex)], peerHalfClosesWhenExhausted: false);
        var serving = Container(new UnitTestAmqp1NodeDirectory()).ServeAsync(connection, Context(new ManualTimeProvider(), log, cancellation.Token));
        await WaitUntilAsync(() => connection.WrittenBytes.Length > Negotiated.Length / 2);

        await cancellation.CancelAsync();
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        var frames = await ReadFramesAsync(connection.WrittenBytes[(Negotiated.Length / 2)..]);
        Assert.IsInstanceOfType<Amqp1Open>(frames[0]);
        var expected = new Amqp1Close(new Amqp1Error(Amqp1ErrorCondition.ResourceLimitExceeded) { Description = "an exchange limit was reached" });
        Assert.AreEqual(Convert.ToHexString(Frame(expected)), Convert.ToHexString(Frame(frames[1])));
        Assert.HasCount(2, frames);
        Assert.IsTrue(connection.WritesCompleted);
        CollectionAssert.Contains(log.Notes.ToList(), "The exchange was cancelled for a limit; closed with amqp:resource-limit-exceeded: an exchange limit was reached.");
    }

    [TestMethod]
    public async Task ServeAsync_CancelledForMaxTimeBeforeOpen_ClosesWithNoBytes()
    {
        using var cancellation = new CancellationTokenSource();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Bytes(SaslHeader)], peerHalfClosesWhenExhausted: false);
        var serving = Container(new UnitTestAmqp1NodeDirectory()).ServeAsync(connection, Context(new ManualTimeProvider(), log, cancellation.Token));
        await WaitUntilAsync(() => connection.WrittenBytes.Length == (SaslHeader + MechanismsPlain).Length / 2);

        await cancellation.CancelAsync();
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(SaslHeader + MechanismsPlain, Convert.ToHexString(connection.WrittenBytes));
        CollectionAssert.Contains(log.Notes.ToList(), "The exchange was cancelled for a limit before open; closed with no bytes.");
    }

    [TestMethod]
    public async Task ServeAsync_CancelledForMaxTimeAfterSaslBeforeOpen_ClosesWithNoBytes()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new InMemoryConnection([Bytes(SaslHeader + InitPlainAliceSecret + AmqpHeader)], peerHalfClosesWhenExhausted: false);
        var serving = Container(new UnitTestAmqp1NodeDirectory()).ServeAsync(connection, Context(new ManualTimeProvider(), new RecordingExchangeLog(), cancellation.Token));
        await WaitUntilAsync(() => connection.WrittenBytes.Length == Negotiated.Length / 2);

        await cancellation.CancelAsync();
        await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(Negotiated, Convert.ToHexString(connection.WrittenBytes));
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_ShutdownDuringSasl_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new InMemoryConnection([Bytes(SaslHeader)], peerHalfClosesWhenExhausted: false);
        var context = Context(new ManualTimeProvider(), new RecordingExchangeLog(), cancellation.Token) with { ShutdownToken = cancellation.Token };
        var serving = Container(new UnitTestAmqp1NodeDirectory()).ServeAsync(connection, context);
        await WaitUntilAsync(() => connection.WrittenBytes.Length == (SaslHeader + MechanismsPlain).Length / 2);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.AreEqual(SaslHeader + MechanismsPlain, Convert.ToHexString(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ShutdownAfterOpen_ThrowsWithNoClose()
    {
        using var cancellation = new CancellationTokenSource();
        var connection = new InMemoryConnection([Bytes(SaslHeader + InitPlainAliceSecret + AmqpHeader + ClientOpenHex)], peerHalfClosesWhenExhausted: false);
        var context = Context(new ManualTimeProvider(), new RecordingExchangeLog(), cancellation.Token) with { ShutdownToken = cancellation.Token };
        var serving = Container(new UnitTestAmqp1NodeDirectory()).ServeAsync(connection, context);
        await WaitUntilAsync(() => connection.WrittenBytes.Length > Negotiated.Length / 2);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsInstanceOfType<Amqp1Open>((await ReadFramesAsync(connection.WrittenBytes[(Negotiated.Length / 2)..])).Single());
    }

    [TestMethod]
    public void Schemes_AreAmqp1AndAmqp1s()
    {
        var container = new Amqp1Container(new UnitTestAmqp1SaslPolicy("PLAIN"), new MessageBus.MessageBus(new ManualTimeProvider()));

        CollectionAssert.AreEqual(new[] { "amqp1", "amqp1s" }, container.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_DefaultNodeDirectory_IsTheBus()
    {
        var bus = new MessageBus.MessageBus(new ManualTimeProvider());
        var attach = new Amqp1Attach("sender-link", 0, IsReceiver: false)
        {
            Target = new Amqp1Described(Amqp1Descriptor.Target, new List<object?> { "orders" }),
        };
        var connection = new InMemoryConnection([Bytes(AmqpHeader + ClientOpenHex), Frame(new Amqp1Begin(0, 100, 100)), Frame(attach)]);
        var container = new Amqp1Container(new UnitTestAmqp1SaslPolicy(), bus, null, new Amqp1SecurityNegotiationOptions { AllowsAnonymous = true });

        await container.ServeAsync(connection, Context(new ManualTimeProvider(), new RecordingExchangeLog(), TestContext.CancellationToken));

        var frames = await ReadFramesAsync(connection.WrittenBytes[(AmqpHeader.Length / 2)..]);
        Assert.IsNotNull(((Amqp1Attach)frames[2]).Target);
        Assert.StartsWith(Amqp1ContainerOptions.ContainerIdPrefix, ((Amqp1Open)frames[0]).ContainerId);
    }

    [TestMethod]
    public async Task ArgumentsNull_Throw()
    {
        var policy = new UnitTestAmqp1SaslPolicy("PLAIN");
        var bus = new MessageBus.MessageBus(new ManualTimeProvider());
        var container = new Amqp1Container(policy, bus);
        var context = Context(new ManualTimeProvider(), new RecordingExchangeLog(), TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp1Container(null!, bus));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Amqp1Container(policy, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => container.ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => container.ServeAsync(new InMemoryConnection([]), null!));
    }

    private static Amqp1Container Container(UnitTestAmqp1NodeDirectory nodes) => new(
        new UnitTestAmqp1SaslPolicy("PLAIN"),
        new MessageBus.MessageBus(new ManualTimeProvider()),
        new Amqp1ContainerOptions { ContainerId = ContainerId },
        null,
        nodes.On);

    private static byte[] Bytes(string hex) => Convert.FromHexString(hex);

    private static byte[] Frame(Amqp1Performative performative) => Amqp1Frame.Carrying(0, performative).Encode();

    private async Task<List<Amqp1Performative>> ReadFramesAsync(byte[] written)
    {
        var reader = new Amqp1FrameReader(new InMemoryConnection([written]));
        List<Amqp1Performative> frames = [];
        while ((await reader.ReadFrameAsync(uint.MaxValue, TestContext.CancellationToken)).Frame is { } frame)
        {
            frames.Add(Amqp1FrameBody.Read(frame).Performative!);
        }

        return frames;
    }

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The container did not reach the expected state in time.");
            }

            await Task.Delay(1, TestContext.CancellationToken);
        }
    }

    private static ExchangeContext Context(TimeProvider clock, IExchangeLog log, CancellationToken cancellation) => new(
        1,
        new ListenUrl("amqp1", "127.0.0.1", 5672).WithBoundPort(5672),
        new IPEndPoint(IPAddress.Loopback, 5672),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        clock,
        cancellation)
    {
        Limits = ExchangeLimits.Default with { HeadTimeout = HeadTimeout },
    };
}
