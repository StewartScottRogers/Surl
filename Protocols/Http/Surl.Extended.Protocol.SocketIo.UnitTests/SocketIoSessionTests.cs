using System.Text;
using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// An Engine.IO session over the WebSocket transport, driven with frames written from RFC 6455 and
/// packets written from EIO ("Handshake", "Heartbeat", "Protocol") and SIO ("Connection to a
/// namespace", "Sending and receiving data", "Acknowledgement", "Disconnection from a namespace",
/// "Packet encoding") (ADR-0120, decisions 2 to 7).
/// </summary>
[TestClass]
public sealed partial class SocketIoSessionTests
{
    private const string TokenPayload = "{\"token\":\"" + UnitTestPasswordPolicy.Token + "\"}";

    private const string BinaryEvent = "451-[\"baz\",{\"_placeholder\":true,\"num\":0}]";

    private static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task RunAsync_Upgrade_SendsTheOpenPacketFirst()
    {
        var harness = new SocketIoServerHarness();

        var client = await SocketIoClient.ConnectAsync(harness);

        Assert.MatchesRegex(OpenPacketPattern(), client.OpenPacket());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_MessageLimitOfZero_IsTheOpenPacketsMaxPayload()
    {
        var harness = new SocketIoServerHarness();
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, maxMessageBytes: 0, 104857600);

        var client = await SocketIoClient.ConnectAsync(harness, harness.Context(limits));

        Assert.EndsWith(",\"maxPayload\":0}", client.OpenPacket());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Connect_MainNamespaceWithAGoodToken_IsAnsweredWithAFreshSid()
    {
        var harness = new SocketIoServerHarness();

        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);

        var answer = client.Messages()[1];
        Assert.MatchesRegex(ConnectAnswerPattern(), answer);
        Assert.DoesNotContain(answer[10..30], client.OpenPacket());
        Assert.AreEqual(new PasswordLogin("socketio", string.Empty, null, null), harness.Policy.Logins.Single() with { Password = null });
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Connect_NamedNamespaceWithAUserNameAndPassword_IsAnsweredOnThatNamespace()
    {
        var harness = new SocketIoServerHarness();

        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/admin", "{\"username\":\"alice\",\"password\":\"wonderland\"}");

        Assert.StartsWith("40/admin,{\"sid\":\"", client.Messages()[1]);
        Assert.AreEqual("alice", harness.Policy.Logins.Single().UserName);
        await client.EndAsync();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("{\"token\":\"wrong\"}")]
    [DataRow("{\"username\":\"alice\",\"password\":\"wrong\"}")]
    public async Task Connect_BadOrMissingAuth_IsConnectErrorAndTheSessionStaysOpen(string payload)
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", payload);

        client.SendText("40" + TokenPayload);

        var messages = await client.WaitForMessagesAsync(3);
        Assert.AreEqual("44{\"message\":\"Not authorized\"}", messages[1]);
        Assert.StartsWith("40{\"sid\":", messages[2]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Connect_AnonymousAllowed_ConnectsWithoutCredentials()
    {
        var harness = new SocketIoServerHarness(allowsAnonymous: true);

        var client = await SocketIoClient.ConnectNamespaceAsync(harness);

        Assert.StartsWith("40{\"sid\":", client.Messages()[1]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Connect_UnknownNamespace_IsConnectErrorWithoutJudgingTheLogin()
    {
        var harness = new SocketIoServerHarness();

        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/other", TokenPayload);

        Assert.AreEqual("44/other,{\"message\":\"Invalid namespace\"}", client.Messages()[1]);
        Assert.IsEmpty(harness.Policy.Logins);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Connect_NamespaceAlreadyConnected_IsIgnored()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);

        client.SendText("40" + TokenPayload);
        client.SendText("42[\"after\"]");

        Assert.AreEqual("42[\"after\"]", (await client.WaitForMessagesAsync(3))[2]);
        Assert.HasCount(1, harness.Policy.Logins);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Event_WithoutAckId_IsEchoedByteForByteOnItsNamespace()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/admin", TokenPayload);

        client.SendText("42/admin,[\"hello\", {\"a\" : 1}]");

        Assert.AreEqual("42/admin,[\"hello\", {\"a\" : 1}]", (await client.WaitForMessagesAsync(3))[2]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Event_WithAnAckId_IsAnsweredWithAnAckCarryingItsArgumentsAndNotEchoed()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);

        client.SendText("421[\"hello\",1]");
        client.SendText("42[\"next\"]");

        var messages = await client.WaitForMessagesAsync(4);
        CollectionAssert.AreEqual(new[] { "431[1]", "42[\"next\"]" }, messages[2..]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Event_BinaryWithAttachment_IsEchoedWithTheSameAttachment()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);

        client.SendText(BinaryEvent);
        client.SendBinary([0x01, 0x02, 0x03]);

        var messages = await client.WaitForMessagesAsync(4);
        CollectionAssert.AreEqual(new[] { BinaryEvent, "<binary:010203>" }, messages[2..]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Event_BinaryWithAnAckId_IsAnsweredWithABinaryAckCarryingTheAttachments()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/admin", TokenPayload);

        client.SendText("452-/admin,15[\"baz\",{\"_placeholder\":true,\"num\":1},{\"_placeholder\":true,\"num\":0}]");
        client.SendBinary([0xAA]);
        client.SendBinary([0xBB, 0xCC]);

        var messages = await client.WaitForMessagesAsync(5);
        CollectionAssert.AreEqual(
            new[] { "462-/admin,15[{\"_placeholder\":true,\"num\":1},{\"_placeholder\":true,\"num\":0}]", "<binary:AA>", "<binary:BBCC>" },
            messages[2..]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Event_NamedBroadcast_ReachesEverySessionOnItsNamespaceOnce()
    {
        var harness = new SocketIoServerHarness();
        var sender = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);
        var listener = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);
        var elsewhere = await SocketIoClient.ConnectNamespaceAsync(harness, "/admin", TokenPayload);

        sender.SendText("423[\"broadcast\",\"hi\"]");

        CollectionAssert.AreEqual(new[] { "42[\"broadcast\",\"hi\"]", "433[\"hi\"]" }, (await sender.WaitForMessagesAsync(4))[2..]);
        Assert.AreEqual("42[\"broadcast\",\"hi\"]", (await listener.WaitForMessagesAsync(3))[2]);
        await sender.EndAsync();
        await listener.EndAsync();
        Assert.HasCount(2, elsewhere.Messages());
        await elsewhere.EndAsync();
    }

    [TestMethod]
    public async Task Event_ForANamespaceNotConnected_IsDropped()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);

        client.SendText("42/admin,[\"dropped\"]");
        client.SendText(BinaryEvent.Replace("451-", "451-/admin,", StringComparison.Ordinal));
        client.SendBinary([0x01]);
        client.SendText("42[\"kept\"]");

        var messages = await client.WaitForMessagesAsync(3);
        Assert.AreEqual("42[\"kept\"]", messages[2]);
        await client.EndAsync();
        Assert.HasCount(3, client.Messages());
    }

    [TestMethod]
    public async Task Ack_FromTheClient_IsReadWithItsAttachmentsAndDropped()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);

        client.SendText("431[1]");
        client.SendText("461-2[{\"_placeholder\":true,\"num\":0}]");
        client.SendBinary([0x01]);
        client.SendText("42[\"kept\"]");

        Assert.AreEqual("42[\"kept\"]", (await client.WaitForMessagesAsync(3))[2]);
        await client.EndAsync();
        Assert.HasCount(3, client.Messages());
    }

    [TestMethod]
    public async Task Disconnect_OneOfTwoNamespaces_LeavesItAndKeepsTheOther()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);
        client.SendText("40/admin," + TokenPayload);
        await client.WaitForMessagesAsync(3);

        client.SendText("41/admin,");
        client.SendText("41/other,");
        client.SendText("42/admin,[\"dropped\"]");
        client.SendText("42[\"kept\"]");

        Assert.AreEqual("42[\"kept\"]", (await client.WaitForMessagesAsync(4))[3]);
        await client.EndAsync();
        Assert.HasCount(4, client.Messages());
    }

    [TestMethod]
    public async Task Disconnect_LastNamespace_StartsTheConnectTimeout()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);
        await harness.Clock.AdvanceWhenWaitingAsync(TimeSpan.FromSeconds(25));
        await client.WaitForMessagesAsync(3);
        client.SendText("3");

        client.SendText("41");
        client.SendText("40/other,");
        await client.WaitForMessagesAsync(4);
        await harness.Clock.AdvanceWhenWaitingAsync(TimeSpan.FromSeconds(20));
        await harness.Clock.AdvanceWhenWaitingAsync(FiveSeconds);
        await client.WaitForMessagesAsync(5);
        await harness.Clock.AdvanceWhenWaitingAsync(FiveSeconds);

        Assert.AreEqual(1000, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(new[] { "2", "44/other,{\"message\":\"Invalid namespace\"}", "2" }, client.Messages()[2..]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_NoNamespaceConnectedWithinTheHeadTimeout_ClosesNormally()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectAsync(harness);

        await harness.Clock.AdvanceWhenWaitingAsync(TimeSpan.FromSeconds(25));
        await harness.Clock.AdvanceWhenWaitingAsync(FiveSeconds);

        Assert.AreEqual(1000, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(new[] { "2" }, client.Messages()[1..]);
        await client.EndAsync();
        Assert.IsTrue(client.Connection.WritesCompleted);
    }

    [TestMethod]
    public async Task RunAsync_InfiniteHeadTimeout_HasNoConnectTimeout()
    {
        var harness = new SocketIoServerHarness();
        var limits = new ExchangeLimits(Timeout.InfiniteTimeSpan, 102400, 8192, 1048576, 104857600);
        var client = await SocketIoClient.ConnectAsync(harness, harness.Context(limits));

        await harness.Clock.AdvanceWhenWaitingAsync(TimeSpan.FromSeconds(25));
        await harness.Clock.AdvanceWhenWaitingAsync(TimeSpan.FromSeconds(20));

        Assert.AreEqual(1000, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_Heartbeat_PingsEveryPingIntervalAfterThePong()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);
        client.SendText("3");

        await harness.Clock.AdvanceWhenWaitingAsync(TimeSpan.FromSeconds(25));
        await client.WaitForMessagesAsync(3);
        client.SendText("3");
        client.SendText("42[\"sync\"]");
        await client.WaitForMessagesAsync(4);
        await harness.Clock.AdvanceWhenWaitingAsync(TimeSpan.FromSeconds(20));
        await harness.Clock.AdvanceWhenWaitingAsync(FiveSeconds);

        CollectionAssert.AreEqual(new[] { "2", "42[\"sync\"]", "2" }, (await client.WaitForMessagesAsync(5))[2..]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_NoPongWithinPingTimeout_ClosesNormally()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);

        await harness.Clock.AdvanceWhenWaitingAsync(TimeSpan.FromSeconds(25));
        client.SendText("2");
        client.SendText("6");
        await harness.Clock.AdvanceWhenWaitingAsync(TimeSpan.FromSeconds(20));

        Assert.AreEqual(1000, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_TextMessagePastMaxPayload_ClosesWithMessageTooBig()
    {
        var harness = new SocketIoServerHarness();
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, maxMessageBytes: 64, 104857600);
        var client = await SocketIoClient.ConnectAsync(harness, harness.Context(limits));

        client.SendText("42[\"" + new string('x', 70) + "\"]");

        Assert.AreEqual(1009, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_FragmentedMessagePastMaxPayload_ClosesWithMessageTooBig()
    {
        var harness = new SocketIoServerHarness();
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, maxMessageBytes: 64, 104857600);
        var client = await SocketIoClient.ConnectAsync(harness, harness.Context(limits));

        client.Connection.Send(SocketIoClient.ClientFrame(0x01, new byte[40]));
        client.Connection.Send(SocketIoClient.ClientFrame(0x80, new byte[40]));

        Assert.AreEqual(1009, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_AttachmentsPastMaxPayload_ClosesWithMessageTooBig()
    {
        var harness = new SocketIoServerHarness(allowsAnonymous: true);
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, maxMessageBytes: 64, 104857600);
        var client = await SocketIoClient.ConnectAsync(harness, harness.Context(limits));
        client.SendText("40");

        client.SendText(BinaryEvent);
        client.SendBinary(new byte[30]);

        Assert.AreEqual(1009, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    [DataRow("42[\"hello\"]", DisplayName = "an event before any CONNECT")]
    [DataRow("0", DisplayName = "a client open")]
    [DataRow("5", DisplayName = "an upgrade outside an upgrade")]
    [DataRow("", DisplayName = "an empty text message")]
    [DataRow("9", DisplayName = "a type character outside 0 to 6")]
    [DataRow("47", DisplayName = "a Socket.IO type outside 0 to 6")]
    [DataRow("40[1]", DisplayName = "a CONNECT payload that is not an object")]
    [DataRow("44{\"message\":\"x\"}", DisplayName = "a client CONNECT_ERROR")]
    public async Task RunAsync_ProtocolErrorBeforeConnecting_ClosesWithProtocolError(string text)
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectAsync(harness);

        client.SendText(text);

        Assert.AreEqual(1002, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
        Assert.HasCount(1, client.Messages());
    }

    [TestMethod]
    [DataRow("42{}", DisplayName = "an EVENT whose payload is not an array")]
    [DataRow("42[1]", DisplayName = "an EVENT whose first element is not a string")]
    [DataRow("451-[\"baz\"]", DisplayName = "a binary event without its placeholder")]
    public async Task RunAsync_MalformedSocketIoPacketAfterConnecting_ClosesWithProtocolError(string text)
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);

        client.SendText(text);

        Assert.AreEqual(1002, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_BinaryMessageWithNoBinaryPacketAwaitingIt_ClosesWithProtocolError()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectAsync(harness);

        client.SendBinary([0x01]);

        Assert.AreEqual(1002, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_TextMessageBeforeEveryAttachment_ClosesWithProtocolError()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);

        client.SendText("452-[\"baz\",{\"_placeholder\":true,\"num\":0},{\"_placeholder\":true,\"num\":1}]");
        client.SendBinary([0x01]);
        client.SendText("42[\"early\"]");

        Assert.AreEqual(1002, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_EngineIoClose_ClosesNormally()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);

        client.SendText("1");

        Assert.AreEqual(1000, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_WebSocketPingAndPong_PingIsAnsweredAndPongIgnored()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectAsync(harness);

        client.Connection.Send(SocketIoClient.ClientFrame(0x8A, [0x09]));
        client.Connection.Send(SocketIoClient.ClientFrame(0x89, [0x07]));

        var bytes = await client.Connection.WaitForWrittenAsync(written => SocketIoClient.Frames(written).Any(frame => frame.Opcode == 0xA));
        CollectionAssert.AreEqual(new byte[] { 0x07 }, SocketIoClient.Frames(bytes).Single(frame => frame.Opcode == 0xA).Payload);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_FragmentedTextMessage_IsReassembled()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);

        client.Connection.Send(SocketIoClient.ClientFrame(0x01, Encoding.ASCII.GetBytes("42[\"hel")));
        client.Connection.Send(SocketIoClient.ClientFrame(0x80, Encoding.ASCII.GetBytes("lo\"]")));

        Assert.AreEqual("42[\"hello\"]", (await client.WaitForMessagesAsync(3))[2]);
        await client.EndAsync();
    }

    [TestMethod]
    [DataRow(new byte[] { 0x88, 0x80, 0x37, 0xfa, 0x21, 0x3d }, 1000, DisplayName = "a WebSocket Close")]
    [DataRow(new byte[] { 0x81, 0x01, 0x34 }, 1002, DisplayName = "an unmasked frame")]
    [DataRow(new byte[] { 0x80, 0x80, 0x37, 0xfa, 0x21, 0x3d }, 1002, DisplayName = "a continuation with no message begun")]
    [DataRow(new byte[] { 0x83, 0x80, 0x37, 0xfa, 0x21, 0x3d }, 1002, DisplayName = "a reserved opcode")]
    [DataRow(new byte[] { 0x81, 0x81, 0x37, 0xfa, 0x21, 0x3d, 0xc8 }, 1007, DisplayName = "text that is not UTF-8")]
    [DataRow(new byte[] { 0x88, 0x83, 0x37, 0xfa, 0x21, 0x3d, 0x34, 0x12, 0xde }, 1007, DisplayName = "a Close reason that is not UTF-8")]
    public async Task RunAsync_WebSocketFrame_EndsWithItsCloseCode(byte[] frame, int closeCode)
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectAsync(harness);

        client.Connection.Send(frame);

        Assert.AreEqual(closeCode, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ClientGoneOrFailing_EndsWithNothingWritten()
    {
        var harness = new SocketIoServerHarness();
        var gone = await SocketIoClient.ConnectAsync(harness);
        var failing = await SocketIoClient.ConnectAsync(harness);

        failing.Connection.FailNextRead(new IOException("reset"));
        await gone.EndAsync();
        await failing.EndAsync();

        Assert.IsFalse(gone.Connection.WritesCompleted);
        Assert.IsFalse(failing.Connection.WritesCompleted);
        Assert.AreEqual(0, harness.Sessions.Count);
    }

    [TestMethod]
    public async Task RunAsync_WriteFails_EndsWithNothingMoreWritten()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);
        var written = client.Connection.WrittenBytes.Length;

        client.Connection.FailWrites();
        client.SendText("42[\"lost\"]");
        await client.Serving.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(written, client.Connection.WrittenBytes.Length);
        Assert.IsTrue(harness.Log.Notes.Any(note => note.Contains("the connection failed (The connection was reset.)", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_ClientPingAndNoop_AreIgnored()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);

        client.SendText("2");
        client.SendText("6");
        client.SendText("42[\"sync\"]");

        Assert.AreEqual("42[\"sync\"]", (await client.WaitForMessagesAsync(3))[2]);
        await client.EndAsync();
        Assert.HasCount(3, client.Messages());
    }

    [TestMethod]
    public async Task Event_BinaryWithNoMessageLimit_IsEchoedWithItsAttachment()
    {
        var harness = new SocketIoServerHarness(allowsAnonymous: true);
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, maxMessageBytes: 0, 104857600);
        var client = await SocketIoClient.ConnectAsync(harness, harness.Context(limits));
        client.SendText("40");

        client.SendText(BinaryEvent);
        client.SendBinary(new byte[200]);

        Assert.AreEqual(BinaryEvent, (await client.WaitForMessagesAsync(4))[2]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task Deliver_MoreThanTheUnsentLimit_ClosesWithPolicyViolation()
    {
        var harness = new SocketIoServerHarness();
        var client = await SocketIoClient.ConnectNamespaceAsync(harness, "/", TokenPayload);
        var big = "42[\"" + new string('x', 600000) + "\"]";

        client.Connection.HoldWrites();
        client.SendText(big);
        client.SendText(big);
        await WaitForNoteAsync(harness, "Frames dropped");
        client.Connection.ReleaseWrites();

        Assert.AreEqual(1008, await client.WaitForCloseCodeAsync());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_CancelledForALimit_SaysDisconnectOnEachNamespaceThenGoingAway()
    {
        var harness = new SocketIoServerHarness();
        using var cancellation = new CancellationTokenSource();
        var client = await SocketIoClient.ConnectAsync(harness, harness.Context(cancellationToken: cancellation.Token));
        client.SendText("40" + TokenPayload);
        client.SendText("40/admin," + TokenPayload);
        await client.WaitForMessagesAsync(3);

        await cancellation.CancelAsync();

        Assert.AreEqual(1001, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(new[] { "41", "41/admin," }, client.Messages()[3..]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_CancelledForALimitWhileWritesAreHeld_GivesUpOnTheFarewellAfterOneSecond()
    {
        var harness = new SocketIoServerHarness();
        using var cancellation = new CancellationTokenSource();
        var client = await SocketIoClient.ConnectAsync(harness, harness.Context(cancellationToken: cancellation.Token));
        client.Connection.HoldWrites();

        await cancellation.CancelAsync();
        await harness.Clock.AdvanceWhenWaitingAsync(SocketIoSession.FarewellWriteDeadline);
        await client.Serving.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.IsTrue(harness.Log.Notes.Any(note => note.Contains("farewell was not written", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_CancelledAtShutdown_WritesNothingMore()
    {
        var harness = new SocketIoServerHarness();
        using var shutdown = new CancellationTokenSource();
        var client = await SocketIoClient.ConnectAsync(harness, harness.Context(cancellationToken: shutdown.Token, shutdownToken: shutdown.Token));
        var written = client.Connection.WrittenBytes.Length;

        await shutdown.CancelAsync();
        await client.Serving.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(written, client.Connection.WrittenBytes.Length);
    }

    // Waits, in real time, for the server to log a note containing the text.
    private static async Task WaitForNoteAsync(SocketIoServerHarness harness, string text)
    {
        var giveUpAt = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!harness.Log.Notes.Any(note => note.Contains(text, StringComparison.Ordinal)))
        {
            Assert.IsLessThan(giveUpAt, DateTime.UtcNow, "The note never came.");
            await Task.Delay(1);
        }
    }

    [GeneratedRegex("^0\\{\"sid\":\"[A-Za-z0-9_-]{20}\",\"upgrades\":\\[\\],\"pingInterval\":25000,\"pingTimeout\":20000,\"maxPayload\":1048576\\}$")]
    private static partial Regex OpenPacketPattern();

    [GeneratedRegex("^40\\{\"sid\":\"[A-Za-z0-9_-]{20}\"\\}$")]
    private static partial Regex ConnectAnswerPattern();
}
