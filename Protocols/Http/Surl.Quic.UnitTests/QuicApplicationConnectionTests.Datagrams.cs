using System.Security.Cryptography;
using System.Text;

namespace Surl.Quic;

/// <summary>
/// <c>DATAGRAM</c> frames through a <see cref="QuicApplicationConnection"/> (BL-753, ADR-0086
/// decision 4). Every byte is written from RFC 9221 section 4: type 0x30 runs to the end of its
/// packet, type 0x31 carries a Length.
/// </summary>
public sealed partial class QuicApplicationConnectionTests
{
    [TestMethod]
    public async Task SendDatagramAsync_ClientOffersDatagramFrames_ReachesTheClientInADatagramFrameWithLength()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient(UnitTestClientHelloEdits.ReplaceParameters(QuicServerConnectionTests.ClientParametersOfferingDatagramFramesOf1024Bytes));
        var protocol = new UnitTestApplicationProtocol(async (connection, token) =>
        {
            await connection.SendDatagramAsync("hello"u8.ToArray(), token);
            await connection.AcceptStreamAsync(CancellationToken.None);
        });

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        await client.UntilAsync(() => client.OneRttFrames<QuicDatagramFrame>().Any(), TestContext.CancellationToken);
        client.SendOneRtt(new QuicConnectionCloseFrame(true, 0, 0, ReadOnlyMemory<byte>.Empty));
        await client.FinishAsync(serving, TestContext.CancellationToken);

        Assert.AreEqual("310568656C6C6F", Convert.ToHexString(client.OneRttFrames<QuicDatagramFrame>().Single().ToBytes()), "Type 0x31, Length 5, \"hello\".");
    }

    [TestMethod]
    public async Task ReceiveDatagramAsync_ClientSendsDatagramFrames_ReturnsEachFramesDataInOrder()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var received = new List<string>();
        var bothReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var protocol = new UnitTestApplicationProtocol(async (connection, token) =>
        {
            received.Add(Encoding.ASCII.GetString((await connection.ReceiveDatagramAsync(token))!));
            received.Add(Encoding.ASCII.GetString((await connection.ReceiveDatagramAsync(token))!));
            bothReceived.SetResult();
            await connection.AcceptStreamAsync(CancellationToken.None);
        });

        var serving = client.Server(DatagramOptions(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        client.Flow.DeliverFromClient(client.Client.SealOneRttPayload(Convert.FromHexString("31026869" + "307468657265")));
        await bothReceived.Task.WaitAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicConnectionCloseFrame(true, 0, 0, ReadOnlyMemory<byte>.Empty));
        await client.FinishAsync(serving, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "hi", "there" }, received);
    }

    [TestMethod]
    public async Task ReceiveDatagramAsync_ConnectionEndsWhileWaiting_ReturnsNullAndSendDatagramAsyncThrowsOperationCanceled()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient(UnitTestClientHelloEdits.ReplaceParameters(QuicServerConnectionTests.ClientParametersOfferingDatagramFramesOf1024Bytes));
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[]? receivedAfterEnd = [];
        Exception? sendAfterEnd = null;
        var protocol = new UnitTestApplicationProtocol(async (connection, _) =>
        {
            var receiving = connection.ReceiveDatagramAsync(CancellationToken.None);
            waiting.SetResult();
            receivedAfterEnd = await receiving;
            try
            {
                await connection.SendDatagramAsync("late"u8.ToArray(), CancellationToken.None);
            }
            catch (Exception exception)
            {
                sendAfterEnd = exception;
            }
        });

        var serving = client.Server(DatagramOptions(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        await waiting.Task.WaitAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicConnectionCloseFrame(true, 0, 0, ReadOnlyMemory<byte>.Empty));
        await client.FinishAsync(serving, TestContext.CancellationToken);

        Assert.IsNull(receivedAfterEnd);
        Assert.IsInstanceOfType<OperationCanceledException>(sendAfterEnd);
        Assert.IsEmpty(client.OneRttFrames<QuicDatagramFrame>().ToList());
    }

    [TestMethod]
    public async Task SendDatagramAsync_ClientOfferedNoMaxDatagramFrameSize_ThrowsInvalidOperation()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        Exception? sent = null;
        var protocol = new UnitTestApplicationProtocol(async (connection, token) =>
        {
            try
            {
                await connection.SendDatagramAsync("hello"u8.ToArray(), token);
            }
            catch (Exception exception)
            {
                sent = exception;
            }
        });

        var serving = client.Server(DatagramOptions(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        await client.FinishAsync(serving, TestContext.CancellationToken);

        Assert.IsInstanceOfType<InvalidOperationException>(sent);
        Assert.IsEmpty(client.OneRttFrames<QuicDatagramFrame>().ToList());
    }

    private static QuicServerConnectionOptions DatagramOptions(RSA key) => new()
    {
        CertificateChain = [UnitTestCertificates.SelfSigned(key)],
        CertificateKey = key,
        MaxDatagramFrameSize = 1200,
    };
}
