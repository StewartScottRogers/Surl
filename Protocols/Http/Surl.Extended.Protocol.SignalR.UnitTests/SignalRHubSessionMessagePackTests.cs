using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A hub connection whose handshake asked for <c>messagepack</c>: binary WebSocket messages carrying
/// VarInt-prefixed MessagePack messages, written by hand from HubProtocol.md's "MessagePack (MsgPack)
/// encoding" and "Binary encoding" (ADR-0119, decision 3).
/// </summary>
[TestClass]
public sealed class SignalRHubSessionMessagePackTests
{
    private const string EchoHi = "96 01 80 a1 31 a4 45 63 68 6f 91 a2 68 69 90";

    private static readonly TimeSpan HundredMilliseconds = TimeSpan.FromMilliseconds(100);

    [TestMethod]
    public async Task RunAsync_MessagePackHandshake_IsAnsweredWithAnEmptyObjectInATextMessage()
    {
        var harness = new SignalRServerHarness();

        var client = await SignalRHubClient.ConnectMessagePackAsync(harness);

        CollectionAssert.AreEqual(new[] { SignalRHubClient.HandshakeResponse }, client.Records());
        Assert.IsEmpty(client.BinaryMessages());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_Echo_IsCompletedInABinaryMessageWithTheArgumentByteForByte()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectMessagePackAsync(harness);

        client.SendBinary(MessagePackHex.Prefixed(EchoHi));

        var messages = await client.WaitForBinaryMessagesAsync(1);
        CollectionAssert.AreEqual(MessagePackHex.Prefixed("95 03 80 a1 31 03 a2 68 69"), messages[0]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_CountStreamInvocation_SendsEachItemAHundredMillisecondsApartThenACompletion()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectMessagePackAsync(harness);

        client.SendBinary(MessagePackHex.Prefixed("96 04 80 a2 73 31 a5 43 6f 75 6e 74 91 03 90"));
        await client.WaitForBinaryMessagesAsync(1);
        await harness.Clock.AdvanceWhenWaitingAsync(HundredMilliseconds);
        await client.WaitForBinaryMessagesAsync(2);
        await harness.Clock.AdvanceWhenWaitingAsync(HundredMilliseconds);

        var messages = await client.WaitForBinaryMessagesAsync(4);
        CollectionAssert.AreEqual(MessagePackHex.Prefixed("94 02 80 a2 73 31 01"), messages[0]);
        CollectionAssert.AreEqual(MessagePackHex.Prefixed("94 02 80 a2 73 31 02"), messages[1]);
        CollectionAssert.AreEqual(MessagePackHex.Prefixed("94 02 80 a2 73 31 03"), messages[2]);
        CollectionAssert.AreEqual(MessagePackHex.Prefixed("94 03 80 a2 73 31 02"), messages[3]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_Fail_IsCompletedWithTheFixedErrorInMessagePack()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectMessagePackAsync(harness);

        client.SendBinary(MessagePackHex.Prefixed("96 01 80 a1 31 a4 46 61 69 6c 90 90"));

        var messages = await client.WaitForBinaryMessagesAsync(1);
        Assert.IsTrue(HubMessagePackReader.TryRead(MessagePackHex.Unprefixed(messages[0]), out var message));
        Assert.AreEqual(new HubCompletion("1", null, SignalRHubMethods.FailText), message);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_MessageSplitAcrossWebSocketMessages_IsReassembled()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectMessagePackAsync(harness);
        var echo = MessagePackHex.Prefixed(EchoHi);

        client.SendBinary(echo[..1]);
        client.SendBinary(echo[1..8]);
        client.SendBinary(echo[8..]);

        var messages = await client.WaitForBinaryMessagesAsync(1);
        CollectionAssert.AreEqual(MessagePackHex.Prefixed("95 03 80 a1 31 03 a2 68 69"), messages[0]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_TwoMessagesInOneWebSocketMessage_AreReadSeparately()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectMessagePackAsync(harness);
        byte[] second = [.. MessagePackHex.Prefixed("96 01 80 a1 32 a4 45 63 68 6f 91 a2 68 69 90")];

        client.SendBinary([.. MessagePackHex.Prefixed(EchoHi), .. second]);

        var messages = await client.WaitForBinaryMessagesAsync(2);
        CollectionAssert.AreEqual(MessagePackHex.Prefixed("95 03 80 a1 31 03 a2 68 69"), messages[0]);
        CollectionAssert.AreEqual(MessagePackHex.Prefixed("95 03 80 a1 32 03 a2 68 69"), messages[1]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ClientPingThenClose_GetsNoAnswerAndClosesNormally()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectMessagePackAsync(harness);

        client.SendBinary(MessagePackHex.Prefixed("91 06"));
        client.SendBinary(MessagePackHex.Prefixed("93 07 c0 c2"));

        Assert.AreEqual(1000, await client.WaitForCloseCodeAsync());
        Assert.IsEmpty(client.BinaryMessages());
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ServerSendsNothingForAKeepAliveInterval_SendsABinaryPing()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectMessagePackAsync(harness);

        await harness.Clock.AdvanceWhenWaitingAsync(SignalRHubSession.KeepAliveInterval);

        var messages = await client.WaitForBinaryMessagesAsync(1);
        CollectionAssert.AreEqual(MessagePackHex.Prefixed("91 06"), messages[0]);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_MessageThatIsNotAMessage_IsAnsweredWithTheErrorCloseInMessagePackThen1002()
    {
        var harness = new SignalRServerHarness();
        var client = await SignalRHubClient.ConnectMessagePackAsync(harness);

        client.SendBinary(MessagePackHex.Prefixed("c1"));

        Assert.AreEqual(1002, await client.WaitForCloseCodeAsync());
        var messages = client.BinaryMessages();
        Assert.HasCount(1, messages);
        Assert.IsTrue(HubMessagePackReader.TryRead(MessagePackHex.Unprefixed(messages[0]), out var message));
        Assert.AreEqual(new HubClose(SignalRSessionEnd.ProtocolErrorText, AllowReconnect: false), message);
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_MessagePastMaxMessage_IsClosedWithTheErrorCloseAndMessageTooBig()
    {
        var harness = new SignalRServerHarness();
        var limits = new ExchangeLimits(TimeSpan.FromSeconds(30), 102400, 8192, maxMessageBytes: 64, 104857600);
        var client = await SignalRHubClient.ConnectMessagePackAsync(harness, harness.Context(limits));

        client.SendBinary([0x41]);

        Assert.AreEqual(1009, await client.WaitForCloseCodeAsync());
        Assert.HasCount(1, client.BinaryMessages());
        await client.EndAsync();
    }
}
