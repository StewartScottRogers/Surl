using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Quic;

/// <summary>An application's calls on a <see cref="QuicApplicationConnection"/>, driven through a <see cref="QuicDatagramFlowServer"/> by the test client.</summary>
[TestClass]
public sealed partial class QuicApplicationConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_DataArrivesWhileWaiting_ReturnsItAndTheStreamOperationsReachTheClient()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var firstReadDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unidirectionalWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationsDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = new List<string>();
        var emptyRead = -1;
        QuicStream? accepted = null;
        QuicStream? unidirectional = null;
        QuicStream? acceptedAfterEnd = null;
        QuicStream? second = null;
        var protocol = new UnitTestApplicationProtocol(async (connection, token) =>
        {
            accepted = (await connection.AcceptStreamAsync(token))!;
            var buffer = new byte[16];
            reads.Add(Encoding.ASCII.GetString(buffer, 0, await connection.ReadAsync(accepted, buffer, token)));
            emptyRead = await connection.ReadAsync(accepted, Memory<byte>.Empty, token);
            firstReadDone.SetResult();
            reads.Add(Encoding.ASCII.GetString(buffer, 0, await connection.ReadAsync(accepted, buffer, token)));
            reads.Add(Encoding.ASCII.GetString(buffer, 0, await connection.ReadAsync(accepted, buffer, token)));
            unidirectional = (await connection.OpenUnidirectionalStreamAsync(token))!;
            await connection.WriteAsync(unidirectional, "x"u8.ToArray(), fin: false, token);
            await connection.StopSendingAsync(accepted, 7, token);
            unidirectionalWritten.SetResult();
            second = await connection.AcceptStreamAsync(token);
            await connection.ResetAsync(unidirectional, 9, token);
            operationsDone.SetResult();
            acceptedAfterEnd = await connection.AcceptStreamAsync(CancellationToken.None);
        });

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicStreamFrame(0, 0, "pi"u8.ToArray(), Fin: false));
        await firstReadDone.Task.WaitAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicStreamFrame(0, 2, "ng"u8.ToArray(), Fin: true));
        await unidirectionalWritten.Task.WaitAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicMaxStreamDataFrame(3, 100000));
        client.SendOneRtt(new QuicStreamFrame(4, 0, "z"u8.ToArray(), Fin: true));
        await operationsDone.Task.WaitAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicConnectionCloseFrame(true, 0, 0, ReadOnlyMemory<byte>.Empty));
        await client.FinishAsync(serving, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "pi", "ng", string.Empty }, reads);
        Assert.AreEqual(0, emptyRead);
        Assert.AreEqual(0UL, accepted!.Id);
        Assert.AreEqual(3UL, unidirectional!.Id);
        Assert.AreEqual(4UL, second!.Id);
        Assert.IsNull(acceptedAfterEnd);
        Assert.AreEqual("x", Encoding.ASCII.GetString(client.StreamSent(3).Data));
        Assert.AreEqual(7UL, client.OneRttFrames<QuicStopSendingFrame>().Single(frame => frame.StreamId == 0).ApplicationErrorCode);
        Assert.AreEqual(9UL, client.OneRttFrames<QuicResetStreamFrame>().Single(frame => frame.StreamId == 3).ApplicationErrorCode);
    }

    [TestMethod]
    public async Task StreamCalls_NullStream_ThrowArgumentNull()
    {
        using var key = RSA.Create(2048);
        using var connection = new QuicServerConnection(UnitTestCertificates.Options(key), TimeProvider.System, RandomNumberGenerator.Create());
        using var ending = new CancellationTokenSource();
        var application = new QuicApplicationConnection(connection, new InMemoryDatagramFlow(new byte[] { 0 }), ending);
        var token = TestContext.CancellationToken;

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await application.ReadAsync(null!, new byte[1], token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await application.WriteAsync(null!, new byte[1], fin: false, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await application.ResetAsync(null!, 0, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await application.ResetAtAsync(null!, 0, 0, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await application.StopSendingAsync(null!, 0, token));
    }
}
