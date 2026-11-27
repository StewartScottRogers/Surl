using System.Security.Cryptography;

namespace Surl.Quic;

// A flow a close=graceful drop fault cuts (ADR-0188, amendment 5): the connection is closed with
// CONNECTION_CLOSE of type 0x1c carrying NO_ERROR (RFC 9000 section 20.1).
public sealed partial class QuicDatagramFlowServerTests
{
    [TestMethod]
    public async Task ServeAsync_FlowCutWhileTheApplicationWrites_SendsTheCountThenConnectionCloseNoError()
    {
        const long AfterBytes = 8000;
        const int AnswerLength = 20_000;
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var flow = new UnitTestGracefullyCutDatagramFlow(client.Flow, AfterBytes);
        var protocol = new UnitTestApplicationProtocol(async (connection, token) =>
        {
            var stream = (await connection.AcceptStreamAsync(token))!;
            await connection.WriteAsync(stream, new byte[AnswerLength], fin: true, token);
            await connection.AcceptStreamAsync(token);
        });

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicStreamFrame(0, 0, "get"u8.ToArray(), Fin: true));
        await client.FinishAsync(serving, TestContext.CancellationToken);

        var close = client.OneRttFrames<QuicConnectionCloseFrame>().Single();
        Assert.IsFalse(close.IsApplicationError);
        Assert.AreEqual((ulong)QuicTransportErrorCode.NoError, close.ErrorCode);
        var counted = client.Flow.SentDatagrams.SkipLast(1).Sum(datagram => (long)datagram.Length);
        Assert.AreEqual(AfterBytes - flow.BytesLeft, counted);
        CollectionAssert.AreEqual(flow.ClosingDatagrams.Single(), client.Flow.SentDatagrams[^1]);
        var received = client.StreamSent(0).Data.Length;
        Assert.IsTrue(received is > 0 and < (int)AfterBytes, $"{received} bytes received");
        Assert.IsTrue(protocol.Connection!.Ended.IsCancellationRequested);
    }

    [TestMethod]
    public async Task ServeAsync_ClientSendsAfterTheCut_TheConnectionCloseIsSentAgainAsAClosingDatagram()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var flow = new UnitTestGracefullyCutDatagramFlow(client.Flow, long.MaxValue);
        var protocol = new UnitTestApplicationProtocol(async (connection, token) =>
        {
            var stream = (await connection.AcceptStreamAsync(token))!;
            // The read takes the connection's lock, so the acknowledgement of the stream's packet is sent first.
            await connection.ReadAsync(stream, new byte[4], token);
            flow.BytesLeft = 0;
            await connection.WriteAsync(stream, "pong"u8.ToArray(), fin: true, token);
            await connection.AcceptStreamAsync(token);
        });

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicStreamFrame(0, 0, "ping"u8.ToArray(), Fin: true));
        await client.UntilAsync(() => flow.ClosingDatagrams.Count == 1, TestContext.CancellationToken);
        client.SendOneRtt(new QuicPingFrame());
        await client.UntilAsync(() => flow.ClosingDatagrams.Count == 2, TestContext.CancellationToken);
        await client.FinishAsync(serving, TestContext.CancellationToken);

        CollectionAssert.AreEqual(flow.ClosingDatagrams[0], flow.ClosingDatagrams[1]);
        Assert.AreEqual(0, client.StreamSent(0).Data.Length);
    }

    [TestMethod]
    public async Task ServeAsync_FlowCutBeforeTheHandshakeCompletes_ClosesWithNoErrorAtTheInitialLevel()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var flow = new UnitTestGracefullyCutDatagramFlow(client.Flow, 100);
        var protocol = new UnitTestApplicationProtocol((_, _) => Task.CompletedTask);

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(flow, TestContext.CancellationToken);
        await client.FinishAsync(serving, TestContext.CancellationToken);

        var close = client.Client.FramesReceived[QuicPacketType.Initial].OfType<QuicConnectionCloseFrame>().First();
        Assert.IsFalse(close.IsApplicationError);
        Assert.AreEqual((ulong)QuicTransportErrorCode.NoError, close.ErrorCode);
        var counted = client.Flow.SentDatagrams.SkipLast(flow.ClosingDatagrams.Count).Sum(datagram => (long)datagram.Length);
        Assert.AreEqual(100 - flow.BytesLeft, counted);
        Assert.IsNotEmpty(flow.ClosingDatagrams);
        Assert.AreEqual(0, protocol.Calls);
    }

    [TestMethod]
    public async Task ServeAsync_TheApplicationsOwnCloseWouldPassTheCount_ItIsSentUncountedAndCutsNothing()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var flow = new UnitTestGracefullyCutDatagramFlow(client.Flow, long.MaxValue);
        var protocol = new UnitTestApplicationProtocol(async (connection, token) =>
        {
            var stream = (await connection.AcceptStreamAsync(token))!;
            await connection.ReadAsync(stream, new byte[4], token);
            flow.BytesLeft = 0;
        });

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicStreamFrame(0, 0, "ping"u8.ToArray(), Fin: true));
        await client.FinishAsync(serving, TestContext.CancellationToken);

        var close = client.OneRttFrames<QuicConnectionCloseFrame>().Single();
        Assert.IsTrue(close.IsApplicationError);
        Assert.AreEqual(0UL, close.ErrorCode);
        Assert.HasCount(1, flow.ClosingDatagrams);
        Assert.IsFalse(flow.IsCut);
    }
}
