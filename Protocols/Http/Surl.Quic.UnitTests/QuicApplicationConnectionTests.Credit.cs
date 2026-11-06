using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>
/// A write holds at most <see cref="QuicApplicationConnection.MaxUnsentLength"/> unsent on a stream
/// and waits for the client's credit to let more go (BL-710, ADR-0082 amendment 3). The client's
/// credit comes from its transport parameters (RFC 9000 section 18.2) and its <c>MAX_STREAM_DATA</c>
/// and <c>MAX_DATA</c> frames (sections 19.9 and 19.10).
/// </summary>
public sealed partial class QuicApplicationConnectionTests
{
    /// <summary>
    /// The recorded client's parameters as they are: <c>initial_max_stream_data_bidi_local</c> 32768
    /// binds and <c>initial_max_data</c> 1048576000 does not.
    /// </summary>
    private const string ClientParametersWhereStreamCreditBinds =
        "050480008000" + "060480008000" + "0704be800000" + "0404be800000" + "080480040000" + "090480040000";

    /// <summary>
    /// The recorded client's parameters with the two credits swapped: <c>initial_max_data</c> 32768
    /// binds and <c>initial_max_stream_data_bidi_local</c> 1048576000 does not.
    /// </summary>
    private const string ClientParametersWhereConnectionCreditBinds =
        "0504be800000" + "060480008000" + "0704be800000" + "040480008000" + "080480040000" + "090480040000";

    /// <summary>The 32768 bytes either set of parameters lets the server send before the client grants more.</summary>
    private const int ClientCredit = 32768;

    [TestMethod]
    [DataRow(ClientParametersWhereStreamCreditBinds, DisplayName = "MAX_STREAM_DATA lets it go")]
    [DataRow(ClientParametersWhereConnectionCreditBinds, DisplayName = "MAX_DATA lets it go")]
    public async Task WriteAsync_MoreThanTheBoundPastTheClientsCredit_WaitsWithTheBoundUnsentUntilTheClientsCreditLetsItGo(string clientParameters)
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient(UnitTestClientHelloEdits.ReplaceParameters(clientParameters));
        var body = RandomNumberGenerator.GetBytes((2 * QuicApplicationConnection.MaxUnsentLength) + 1000);
        var accepted = new TaskCompletionSource<QuicStream>(TaskCreationOptions.RunContinuationsAsynchronously);
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var protocol = new UnitTestApplicationProtocol(async (connection, token) =>
        {
            var stream = (await connection.AcceptStreamAsync(token))!;
            accepted.SetResult(stream);
            await connection.WriteAsync(stream, body, fin: true, token);
            written.SetResult();
            await connection.AcceptStreamAsync(CancellationToken.None);
        });

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicStreamFrame(0, 0, "go"u8.ToArray(), Fin: false));
        var stream = await accepted.Task.WaitAsync(TestContext.CancellationToken);
        await AcknowledgeUntilAsync(client, () => client.StreamSent(0).Data.Length == ClientCredit && stream.UnsentLength == QuicApplicationConnection.MaxUnsentLength, TestContext.CancellationToken);

        Assert.IsFalse(written.Task.IsCompleted, "The write still holds bytes it has not queued.");
        client.SendOneRtt(clientParameters == ClientParametersWhereStreamCreditBinds ? new QuicMaxStreamDataFrame(0, 1 << 20) : new QuicMaxDataFrame(1 << 20));
        await AcknowledgeUntilAsync(client, () => client.StreamSent(0).Fin, TestContext.CancellationToken);
        await written.Task.WaitAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicConnectionCloseFrame(true, 0, 0, ReadOnlyMemory<byte>.Empty));
        await client.FinishAsync(serving, TestContext.CancellationToken);

        CollectionAssert.AreEqual(body, client.StreamSent(0).Data);
    }

    [TestMethod]
    public async Task WriteAsync_WaitingForCreditWhenTheConnectionEnds_ThrowsOperationCanceled()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient();
        var accepted = new TaskCompletionSource<QuicStream>(TaskCreationOptions.RunContinuationsAsynchronously);
        var outcome = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var testToken = TestContext.CancellationToken;
        var protocol = new UnitTestApplicationProtocol(async (connection, _) =>
        {
            var stream = (await connection.AcceptStreamAsync(testToken))!;
            accepted.SetResult(stream);
            try
            {
                await connection.WriteAsync(stream, new byte[2 * QuicApplicationConnection.MaxUnsentLength], fin: true, testToken);
                outcome.SetResult(null);
            }
            catch (OperationCanceledException canceled)
            {
                outcome.SetResult(canceled);
            }
        });

        var serving = client.Server(UnitTestCertificates.Options(key), protocol).ServeAsync(client.Flow, testToken);
        await client.CompleteHandshakeAsync(testToken);
        client.SendOneRtt(new QuicStreamFrame(0, 0, "go"u8.ToArray(), Fin: false));
        var stream = await accepted.Task.WaitAsync(testToken);
        await AcknowledgeUntilAsync(client, () => client.StreamSent(0).Data.Length == ClientCredit && stream.UnsentLength == QuicApplicationConnection.MaxUnsentLength, testToken);
        Assert.IsFalse(outcome.Task.IsCompleted, "The write waits for credit.");
        client.SendOneRtt(new QuicConnectionCloseFrame(true, 0, 0, ReadOnlyMemory<byte>.Empty));
        await client.FinishAsync(serving, testToken);

        var thrown = await outcome.Task.WaitAsync(testToken);
        Assert.IsInstanceOfType<OperationCanceledException>(thrown);
    }

    /// <summary>Acknowledges every 1-RTT packet the server sends, so its congestion window keeps opening, until <paramref name="condition"/> holds.</summary>
    private static async Task AcknowledgeUntilAsync(UnitTestFlowClient client, Func<bool> condition, CancellationToken cancellationToken)
    {
        var acknowledged = 0;
        while (true)
        {
            await client.UntilAsync(() => condition() || client.Client.PacketNumbersReceived[QuicPacketType.OneRtt].Count > acknowledged, cancellationToken);
            if (condition())
            {
                return;
            }

            acknowledged = client.Client.PacketNumbersReceived[QuicPacketType.OneRtt].Count;
            client.SendOneRtt(client.Client.AckFrame(QuicPacketType.OneRtt));
        }
    }
}
