namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Stateful reconnect over the WebSocket transport, on a <see cref="ManualTimeProvider"/>, driven with
/// the records HubProtocol.md "Ack Message Encoding" and "Sequence Message Encoding" describe
/// (ADR-0119, decision 6).
/// </summary>
[TestClass]
public sealed class SignalRStatefulReconnectTests
{
    private const string ProtocolErrorClose = "{\"type\":7,\"error\":\"Connection closed with an error.\"}\u001e";

    private const string EchoOne = "{\"type\":1,\"invocationId\":\"1\",\"target\":\"Echo\",\"arguments\":[\"a\"]}";

    private const string EchoTwo = "{\"type\":1,\"invocationId\":\"2\",\"target\":\"Echo\",\"arguments\":[\"b\"]}";

    private const string CompletionOne = "{\"type\":3,\"invocationId\":\"1\",\"result\":\"a\"}\u001e";

    private const string CompletionTwo = "{\"type\":3,\"invocationId\":\"2\",\"result\":\"b\"}\u001e";

    private static string Ack(int sequenceId) => "{\"type\":8,\"sequenceId\":" + sequenceId + "}\u001e";

    private static string Sequence(int sequenceId) => "{\"type\":9,\"sequenceId\":" + sequenceId + "}\u001e";

    private static string Target(SignalRNegotiatedConnection connection) => "/chat?id=" + Uri.EscapeDataString(connection.UpgradeId);

    private static SignalRNegotiatedConnection Negotiate(SignalRServerHarness harness) =>
        harness.Connections.Negotiate(string.Empty, true, TimeSpan.FromSeconds(30), isStateful: true)!;

    private static Task<SignalRHubClient> ConnectAsync(SignalRServerHarness harness, SignalRNegotiatedConnection connection) =>
        SignalRHubClient.ConnectAsync(harness, target: Target(connection));

    [TestMethod]
    public async Task RunAsync_NumberedClientMessage_IsAcknowledgedBeforeItsCompletionAndAPingIsNot()
    {
        var harness = new SignalRServerHarness();
        var client = await ConnectAsync(harness, Negotiate(harness));

        client.SendRecords("{\"type\":6}", EchoOne, EchoTwo);

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, Ack(1), CompletionOne, Ack(2), CompletionTwo },
            await client.WaitForRecordsAsync(5));
        await client.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ReconnectWithinThirtySeconds_AnswersHandshakeThenSequenceAndReplaysUnacknowledgedRecordsInOrder()
    {
        var harness = new SignalRServerHarness();
        var negotiated = Negotiate(harness);
        var first = await ConnectAsync(harness, negotiated);
        first.SendRecords(EchoOne, EchoTwo);
        await first.WaitForRecordsAsync(5);
        await first.EndAsync();
        Assert.AreEqual(1, harness.Connections.Count);
        harness.Clock.Advance(TimeSpan.FromSeconds(29));

        var second = await ConnectAsync(harness, negotiated);

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, Sequence(1), CompletionOne, CompletionTwo },
            await second.WaitForRecordsAsync(4));
        await second.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ClientAck_FreesTheRecordsItCoversSoOnlyLaterOnesAreReplayed()
    {
        var harness = new SignalRServerHarness();
        var negotiated = Negotiate(harness);
        var first = await ConnectAsync(harness, negotiated);
        first.SendRecords(EchoOne, EchoTwo, "{\"type\":8,\"sequenceId\":1}", "{\"type\":8,\"sequenceId\":0}", "{\"type\":1,\"invocationId\":\"3\",\"target\":\"Echo\",\"arguments\":[\"c\"]}");
        await first.WaitForRecordsAsync(7);
        await first.EndAsync();

        var second = await ConnectAsync(harness, negotiated);

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, Sequence(2), CompletionTwo, "{\"type\":3,\"invocationId\":\"3\",\"result\":\"c\"}\u001e" },
            await second.WaitForRecordsAsync(4));
        await second.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ReconnectWithNothingUnacknowledged_SequenceNamesTheNextRecord()
    {
        var harness = new SignalRServerHarness();
        var negotiated = Negotiate(harness);
        var first = await ConnectAsync(harness, negotiated);
        await first.EndAsync();

        var second = await ConnectAsync(harness, negotiated);

        CollectionAssert.AreEqual(new[] { SignalRHubClient.HandshakeResponse, Sequence(1) }, await second.WaitForRecordsAsync(2));
        await second.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ClientSequence_DropsTheMessagesItSendsAgain()
    {
        var harness = new SignalRServerHarness();
        var negotiated = Negotiate(harness);
        var first = await ConnectAsync(harness, negotiated);
        first.SendRecords(EchoOne);
        await first.WaitForRecordsAsync(3);
        await first.EndAsync();
        var second = await ConnectAsync(harness, negotiated);

        second.SendRecords("{\"type\":9,\"sequenceId\":1}", EchoOne, EchoTwo);

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, Sequence(1), CompletionOne, Ack(2), CompletionTwo },
            await second.WaitForRecordsAsync(5));
        await second.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ClientSequenceAheadOfWhatWasReceived_DropsNothing()
    {
        var harness = new SignalRServerHarness();
        var negotiated = Negotiate(harness);
        var first = await ConnectAsync(harness, negotiated);
        await first.EndAsync();
        var second = await ConnectAsync(harness, negotiated);

        second.SendRecords("{\"type\":9,\"sequenceId\":7}", EchoOne);

        Assert.AreEqual(Ack(1), (await second.WaitForRecordsAsync(4))[2]);
        await second.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_ReconnectAfterThirtySeconds_IsNotFound()
    {
        var harness = new SignalRServerHarness();
        var negotiated = Negotiate(harness);
        var first = await ConnectAsync(harness, negotiated);
        await first.EndAsync();
        harness.Clock.Advance(SignalRHubConnections.ReconnectWindow);

        var response = await harness.SendRawAsync(SignalRHubClient.UpgradeRequest(Target(negotiated)));

        Assert.StartsWith("HTTP/1.1 404 Not Found\r\n", response);
        Assert.AreEqual(0, harness.Connections.Count);
    }

    [TestMethod]
    public async Task RunAsync_ReconnectWhileTheFirstWebSocketIsUp_IsConflict()
    {
        var harness = new SignalRServerHarness();
        var negotiated = Negotiate(harness);
        var first = await ConnectAsync(harness, negotiated);

        var response = await harness.SendRawAsync(SignalRHubClient.UpgradeRequest(Target(negotiated)));

        Assert.StartsWith("HTTP/1.1 409 Conflict\r\n", response);
        await first.EndAsync();
    }

    [TestMethod]
    public async Task RunAsync_UnacknowledgedRecordsPastTheBound_EndsTheConnectionWithTheProtocolError()
    {
        var harness = new SignalRServerHarness();
        var negotiated = Negotiate(harness);
        var client = await ConnectAsync(harness, negotiated);
        var echo = "{\"type\":1,\"invocationId\":\"big\",\"target\":\"Echo\",\"arguments\":[\"" + new string('x', 60000) + "\"]}";

        client.SendRecords(echo);
        await client.WaitForRecordsAsync(3);
        client.SendRecords(echo);

        Assert.AreEqual(1002, await client.WaitForCloseCodeAsync());
        Assert.AreEqual(ProtocolErrorClose, (await client.WaitForRecordsAsync(5))[4]);
        await client.EndAsync();
        Assert.AreEqual(0, harness.Connections.Count);
    }

    [TestMethod]
    public async Task RunAsync_AckAboveTheLastRecordSent_IsAProtocolError()
    {
        var harness = new SignalRServerHarness();
        var client = await ConnectAsync(harness, Negotiate(harness));

        client.SendRecords("{\"type\":8,\"sequenceId\":1}");

        Assert.AreEqual(1002, await client.WaitForCloseCodeAsync());
        CollectionAssert.AreEqual(new[] { SignalRHubClient.HandshakeResponse, ProtocolErrorClose }, client.Records());
        await client.EndAsync();
        Assert.AreEqual(0, harness.Connections.Count);
    }

    [TestMethod]
    public async Task RunAsync_HubCloseFromTheClient_EndsTheConnectionForGood()
    {
        var harness = new SignalRServerHarness();
        var client = await ConnectAsync(harness, Negotiate(harness));

        client.SendRecords("{\"type\":7}");
        await client.WaitForCloseCodeAsync();
        await client.EndAsync();

        Assert.AreEqual(0, harness.Connections.Count);
    }

    [TestMethod]
    public async Task RunAsync_CountStream_NumbersItsItemsAndCompletion()
    {
        var harness = new SignalRServerHarness();
        var negotiated = Negotiate(harness);
        var first = await ConnectAsync(harness, negotiated);
        first.SendRecords("{\"type\":4,\"invocationId\":\"s\",\"target\":\"Count\",\"arguments\":[1]}");
        await first.WaitForRecordsAsync(4);
        await first.EndAsync();

        var second = await ConnectAsync(harness, negotiated);

        CollectionAssert.AreEqual(
            new[] { SignalRHubClient.HandshakeResponse, Sequence(1), "{\"type\":2,\"invocationId\":\"s\",\"item\":1}\u001e", "{\"type\":3,\"invocationId\":\"s\"}\u001e" },
            await second.WaitForRecordsAsync(4));
        await second.EndAsync();
    }
}
