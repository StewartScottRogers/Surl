using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Surl.MessageBus;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The broker's answers to <c>CONNECT</c> and what follows it (BL-496; ADR-0093 decisions 1, 2,
/// 6 and 8), every expected byte written from MQTT 5.0.
/// </summary>
[TestClass]
public sealed partial class Mqtt5BrokerTests
{
    // CONNECT: "MQTT" level 5, flags C2 (user name, password, Clean Start), Keep Alive 30, no
    // properties, client "c1", user "alice", password "secret".
    private static readonly byte[] ConnectAsAlice =
    [
        0x10, 0x1E,
        0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, 0x05, 0xC2, 0x00, 0x1E, 0x00,
        0x00, 0x02, 0x63, 0x31,
        0x00, 0x05, 0x61, 0x6C, 0x69, 0x63, 0x65,
        0x00, 0x06, 0x73, 0x65, 0x63, 0x72, 0x65, 0x74,
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted);

        Assert.ThrowsExactly<ArgumentNullException>(() => new Mqtt5Broker(null!, policy));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Mqtt5Broker(NewState(), null!));
    }

    [TestMethod]
    public void Schemes_AreMqtt5AndMqtt5s()
    {
        CollectionAssert.AreEqual(new[] { "mqtt5", "mqtt5s" }, Broker().Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Broker().ServeAsync(null!, context));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Broker().ServeAsync(ConnectionSending(false), null!));
    }

    [TestMethod]
    public async Task ServeAsync_ConnectWithGoodCredentials_AnswersSuccessWithTheAdrsProperties()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Broker(policy: policy), Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log), ConnectAsAlice, DisconnectNormally);

        CollectionAssert.AreEqual(ConnackSuccess, connection.WrittenBytes);
        var login = policy.Logins.Single();
        Assert.AreEqual("mqtt5", login.Scheme);
        Assert.AreEqual("alice", login.UserName);
        CollectionAssert.AreEqual("secret"u8.ToArray(), login.Password!.Value.ToArray());
        Assert.IsNull(login.TlsSession);
        Assert.AreEqual("Login accepted: mqtt5 alice", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_BadPassword_AnswersBadUserNameOrPasswordAndCloses()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.RefusedCredentials);
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Broker(policy: policy), Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log), ConnectAsAlice, PingRequest);

        CollectionAssert.AreEqual(ConnackRefused(0x86), connection.WrittenBytes);
        CollectionAssert.AreEqual(
            new[] { "Login refused: mqtt5 alice", "CONNECT refused (3.1.3.5); answered CONNACK 0x86 and closed." },
            log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow(PasswordLoginVerdict.RefusedAnonymous, DisplayName = "No credentials with anonymous off")]
    [DataRow(PasswordLoginVerdict.RefusedPlaintext, DisplayName = "A password over a connection with no TLS")]
    public async Task ServeAsync_LoginRefusedUnchecked_AnswersNotAuthorized(PasswordLoginVerdict verdict)
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(verdict);

        var connection = await ServeAsync(Broker(policy: policy), Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect());

        CollectionAssert.AreEqual(ConnackRefused(0x87), connection.WrittenBytes);
        var login = policy.Logins.Single();
        Assert.IsNull(login.UserName);
        Assert.IsNull(login.Password);
    }

    [TestMethod]
    public async Task ServeAsync_LoginOverTls_PassesTheTlsSession()
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted);
        var connection = new InMemoryConnection([ConnectAsAlice], initialTlsSession: InMemoryConnection.DefaultUpgradeTlsSession);

        await Broker(policy: policy).ServeAsync(connection, Context(new ManualTimeProvider(), TestContext.CancellationToken));

        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, policy.Logins.Single().TlsSession);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x10, 0x0D, 0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, 0x04, 0x02, 0x00, 0x3C, 0x00, 0x01, 0x63 }, DisplayName = "MQTT level 4")]
    [DataRow(new byte[] { 0x10, 0x0F, 0x00, 0x06, 0x4D, 0x51, 0x49, 0x73, 0x64, 0x70, 0x03, 0x02, 0x00, 0x3C, 0x00, 0x01, 0x63 }, DisplayName = "MQIsdp level 3")]
    public async Task ServeAsync_ProtocolLevelNot5_AnswersUnsupportedProtocolVersion(byte[] connect)
    {
        var connection = await ServeAsync(Broker(), Context(new ManualTimeProvider(), TestContext.CancellationToken), connect);

        CollectionAssert.AreEqual(ConnackRefused(0x84), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectWithTheReservedFlag_AnswersMalformedPacket()
    {
        byte[] connect = [0x10, 0x0E, 0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, 0x05, 0x03, 0x00, 0x3C, 0x00, 0x00, 0x01, 0x63];

        var connection = await ServeAsync(Broker(), Context(new ManualTimeProvider(), TestContext.CancellationToken), connect);

        CollectionAssert.AreEqual(ConnackRefused(0x81), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectOverThePacketLimit_AnswersPacketTooLarge()
    {
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 10 };

        var connection = await ServeAsync(Broker(), Context(new ManualTimeProvider(), TestContext.CancellationToken, limits), ConnectAsAlice);

        CollectionAssert.AreEqual(ConnackRefused(0x95), connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_AuthenticationMethod_AnswersBadAuthenticationMethod()
    {
        var connect = Connect(properties: [Mqtt5Property.FromText(Mqtt5PropertyIdentifier.AuthenticationMethod, "SCRAM-SHA-256")]);

        var connection = await ServeAsync(Broker(), Context(new ManualTimeProvider(), TestContext.CancellationToken), connect);

        CollectionAssert.AreEqual(ConnackRefused(0x8C), connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x10, 0x0E, 0x00, 0x04, 0x48, 0x54, 0x54, 0x50, 0x05, 0x02, 0x00, 0x3C, 0x00, 0x00, 0x01, 0x63 }, "named no MQTT protocol ([MQTT-3.1.2-1])", DisplayName = "A protocol that is not MQTT")]
    [DataRow(new byte[] { 0xC0, 0x00 }, "The first packet was PingRequest, not CONNECT", DisplayName = "A packet before CONNECT")]
    [DataRow(new byte[] { 0x60, 0x02, 0x00, 0x01 }, "was not CONNECT", DisplayName = "A refused packet before CONNECT")]
    [DataRow(new byte[] { 0x10, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }, "The first packet's fixed header was unreadable", DisplayName = "An unreadable fixed header")]
    [DataRow(new byte[] { }, "The client closed the connection before CONNECT", DisplayName = "Nothing")]
    public async Task ServeAsync_FirstPacketNotAnAnswerableConnect_ClosesWithNoBytes(byte[] first, string expectedNote)
    {
        var log = new RecordingExchangeLog();

        var connection = await ServeAsync(Broker(), Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log), first);

        Assert.IsEmpty(connection.WrittenBytes);
        StringAssert.Contains(log.Notes.Single(), expectedNote);
        StringAssert.EndsWith(log.Notes.Single(), "; closed with no reply.");
    }

    [TestMethod]
    public async Task ServeAsync_ZeroLengthClientIdentifier_AssignsOneInConnack()
    {
        var connection = await ServeAsync(Broker(), Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect(clientIdentifier: string.Empty));

        var written = connection.WrittenBytes;
        var connack = (Mqtt5ConnectAcknowledgementPacket)Mqtt5PacketDecoder.Decode(written[0], written.AsMemory(2)).Packet!;
        Assert.AreEqual(Mqtt5ReasonCode.Success, connack.ReasonCode);
        Assert.IsFalse(connack.SessionPresent);
        CollectionAssert.AreEqual(
            new[] { Mqtt5PropertyIdentifier.ReceiveMaximum, Mqtt5PropertyIdentifier.MaximumPacketSize, Mqtt5PropertyIdentifier.AssignedClientIdentifier, Mqtt5PropertyIdentifier.TopicAliasMaximum },
            connack.Properties.Select(property => property.Identifier).ToArray());
        Assert.IsTrue(AssignedClientIdentifierPattern().IsMatch(connack.Properties[2].Text));
    }

    [TestMethod]
    [DataRow((ushort)0, DisplayName = "Keep Alive 0")]
    [DataRow((ushort)61, DisplayName = "Keep Alive 61")]
    public async Task ServeAsync_LongSessionExpiryKeepAliveNotStandingAndNoPacketLimit_ConnackCapsAndSetsThem(ushort keepAlive)
    {
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 0 };
        var connect = Connect(keepAlive: keepAlive, properties: [SessionExpiry(7200)]);

        var connection = await ServeAsync(Broker(), Context(new ManualTimeProvider(), TestContext.CancellationToken, limits), connect);

        byte[] expected =
        [
            0x20, 0x11, 0x00, 0x00, 0x0E,
            0x11, 0x00, 0x00, 0x0E, 0x10,
            0x21, 0x00, 0x64,
            0x22, 0x00, 0x0A,
            0x13, 0x00, 0x3C,
        ];
        CollectionAssert.AreEqual(expected, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_SecondConnect_DisconnectsWithProtocolError()
    {
        var connection = await ServeAsync(Broker(), Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect(), Connect(), PingRequest);

        CollectionAssert.AreEqual(Disconnect(0x82), AfterConnackSuccess(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_MalformedSecondConnect_DisconnectsWithProtocolError()
    {
        byte[] malformedConnect = [0x10, 0x0E, 0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, 0x05, 0x03, 0x00, 0x3C, 0x00, 0x00, 0x01, 0x63];

        var connection = await ServeAsync(Broker(), Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect(), malformedConnect);

        CollectionAssert.AreEqual(Disconnect(0x82), AfterConnackSuccess(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_PingRequest_AnswersPingResponse()
    {
        var connection = await ServeAsync(Broker(), Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect(), PingRequest, PingRequest);

        CollectionAssert.AreEqual(new byte[] { 0xD0, 0x00, 0xD0, 0x00 }, AfterConnackSuccess(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow(new byte[] { 0xD0, 0x00 }, DisplayName = "PINGRESP")]
    [DataRow(new byte[] { 0x20, 0x03, 0x00, 0x00, 0x00 }, DisplayName = "CONNACK")]
    [DataRow(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, DisplayName = "SUBACK")]
    [DataRow(new byte[] { 0xB0, 0x04, 0x00, 0x01, 0x00, 0x00 }, DisplayName = "UNSUBACK")]
    [DataRow(new byte[] { 0xF0, 0x00 }, DisplayName = "AUTH with no method in use")]
    public async Task ServeAsync_PacketAClientNeverSends_DisconnectsWithProtocolError(byte[] packet)
    {
        var connection = await ServeAsync(Broker(), Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect(), packet);

        CollectionAssert.AreEqual(Disconnect(0x82), AfterConnackSuccess(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_RefusedPacketAfterConnack_DisconnectsWithItsReasonCode()
    {
        var connection = await ServeAsync(Broker(), Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect(), [0x60, 0x02, 0x00, 0x01]);

        CollectionAssert.AreEqual(Disconnect(0x81), AfterConnackSuccess(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task ServeAsync_SilencePastOneAndAHalfTimesTheKeepAlive_DisconnectsWithKeepAliveTimeout()
    {
        var clock = new ManualTimeProvider();
        var published = PublishedBy(out var state);
        var connection = ConnectionSending(staysOpen: true, Connect(keepAlive: 10, will: Will()));

        var serving = Broker(state).ServeAsync(connection, Context(clock, TestContext.CancellationToken));
        clock.Advance(TimeSpan.FromSeconds(15) - TimeSpan.FromTicks(1));
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        CollectionAssert.AreEqual(Disconnect(0x8D), AfterConnackSuccess(connection.WrittenBytes));
        Assert.HasCount(1, published);
    }

    [TestMethod]
    public async Task ServeAsync_ClientCloses_LeavesNoTimerPending()
    {
        var clock = new ManualTimeProvider();
        var connection = ConnectionSending(staysOpen: false, Connect(keepAlive: 10));

        await Broker().ServeAsync(connection, Context(clock, TestContext.CancellationToken));

        CollectionAssert.AreEqual(ConnackSuccess, connection.WrittenBytes);
        Assert.AreEqual(0, clock.PendingTimerCount);
    }

    [TestMethod]
    public async Task ServeAsync_PartialConnectPastTheHeadTimeout_ClosesWithNoBytes()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = ConnectionSending(staysOpen: true, [0x10]);

        var serving = Broker().ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        clock.Advance(ExchangeLimits.Default.HeadTimeout);
        await serving;

        Assert.IsEmpty(connection.WrittenBytes);
        Assert.AreEqual("The first packet was not complete within the head timeout; closed with no reply.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_InfiniteHeadTimeout_WaitsUntilTheExchangeIsCancelled()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var limits = ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan };
        var connection = ConnectionSending(staysOpen: true, [0x10]);

        var serving = Broker().ServeAsync(connection, Context(clock, idleTimeout.Token, limits, log));
        clock.Advance(TimeSpan.FromDays(1));
        Assert.IsFalse(serving.IsCompleted);
        await idleTimeout.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsEmpty(connection.WrittenBytes);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledBeforeConnect_IsNotTakenForAHeadTimeout()
    {
        var log = new RecordingExchangeLog();
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = ConnectionSending(staysOpen: true, [0x10]);

        var serving = Broker().ServeAsync(connection, Context(new ManualTimeProvider(), idleTimeout.Token, log: log));
        await idleTimeout.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsEmpty(log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledWhileConnected_PublishesTheWill()
    {
        var published = PublishedBy(out var state);
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = ConnectionSending(staysOpen: true, Connect(will: Will()));

        var serving = Broker(state).ServeAsync(connection, Context(new ManualTimeProvider(), idleTimeout.Token));
        await idleTimeout.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.HasCount(1, published);
        Assert.IsFalse(state.IsConnected("c1"));
    }

    [TestMethod]
    public async Task ServeAsync_AbnormalCloseWithAWillDelay_PublishesTheWillAfterItsDelay()
    {
        var clock = new ManualTimeProvider();
        var published = PublishedBy(out var state);
        var will = Will(WillDelay(5), Mqtt5Property.FromUserProperty("k", "v"));

        await ServeAsync(Broker(state), Context(clock, TestContext.CancellationToken), Connect(properties: [SessionExpiry(10)], will: will));
        clock.Advance(TimeSpan.FromSeconds(5) - TimeSpan.FromTicks(1));
        Assert.IsEmpty(published);
        clock.Advance(TimeSpan.FromTicks(1));

        var message = published.Single();
        Assert.AreEqual("w", message.Topic);
        CollectionAssert.AreEqual("gone"u8.ToArray(), message.Payload.ToArray());
        Assert.AreEqual(BusQualityOfService.AtLeastOnce, message.QualityOfService);
        Assert.IsTrue(message.Retain);
        Assert.AreEqual(new BusMessageHeader("k", "v"), message.Headers.Single());
    }

    [TestMethod]
    [DataRow(0u, 10u, DisplayName = "No Will Delay")]
    [DataRow(5u, 0u, DisplayName = "A Will Delay past a session that ends with the connection")]
    public async Task ServeAsync_AbnormalCloseWithNoDelayToWait_PublishesTheWillAtOnce(uint willDelaySeconds, uint sessionExpirySeconds)
    {
        var published = PublishedBy(out var state);
        var connect = Connect(properties: [SessionExpiry(sessionExpirySeconds)], will: Will(WillDelay(willDelaySeconds)));

        await ServeAsync(Broker(state), Context(new ManualTimeProvider(), TestContext.CancellationToken), connect);

        Assert.HasCount(1, published);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30 }, DisplayName = "A close part way through a packet")]
    [DataRow(new byte[] { 0xE0, 0x01, 0x04 }, DisplayName = "DISCONNECT 0x04 Disconnect with Will Message")]
    public async Task ServeAsync_EndWithoutANormalDisconnect_PublishesTheWill(byte[] last)
    {
        var published = PublishedBy(out var state);

        var connection = await ServeAsync(Broker(state), Context(new ManualTimeProvider(), TestContext.CancellationToken), Connect(will: Will()), last);

        CollectionAssert.AreEqual(ConnackSuccess, connection.WrittenBytes);
        Assert.HasCount(1, published);
    }

    [TestMethod]
    public async Task ServeAsync_NormalDisconnect_DiscardsTheWill()
    {
        var clock = new ManualTimeProvider();
        var published = PublishedBy(out var state);

        await ServeAsync(Broker(state), Context(clock, TestContext.CancellationToken), Connect(will: Will()), DisconnectNormally);
        clock.Advance(TimeSpan.FromHours(2));

        Assert.IsEmpty(published);
        Assert.IsFalse(state.IsConnected("c1"));
    }

    [TestMethod]
    [DataRow(true, 1, DisplayName = "Clean Start 1 ends the old session: the Will is published")]
    [DataRow(false, 0, DisplayName = "Clean Start 0 continues it: the Will is discarded")]
    public async Task ServeAsync_ConnectWhileAWillWaits_SettlesIt(bool cleanStart, int expectedPublished)
    {
        var clock = new ManualTimeProvider();
        var published = PublishedBy(out var state);
        var broker = Broker(state);
        await ServeAsync(broker, Context(clock, TestContext.CancellationToken), Connect(properties: [SessionExpiry(60)], will: Will(WillDelay(30))));

        await ServeAsync(broker, Context(clock, TestContext.CancellationToken), Connect(cleanStart: cleanStart), DisconnectNormally);
        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.HasCount(expectedPublished, published);
    }

    private static ConcurrentQueue<BusMessage> PublishedBy(out Mqtt5BrokerState state)
    {
        state = NewState();
        return PublishedOn(state);
    }

    [GeneratedRegex("^surl-[0-9a-f]{32}$")]
    private static partial Regex AssignedClientIdentifierPattern();
}
