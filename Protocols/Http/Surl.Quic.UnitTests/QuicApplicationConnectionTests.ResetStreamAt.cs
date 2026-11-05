using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>
/// <c>RESET_STREAM_AT</c> through a <see cref="QuicApplicationConnection"/> (BL-748). Every byte is
/// written from draft-ietf-quic-reliable-stream-reset-09 section 4: type 0x24, Stream ID,
/// Application Protocol Error Code, Final Size, Reliable Size.
/// </summary>
public sealed partial class QuicApplicationConnectionTests
{
    [TestMethod]
    public async Task ResetAtAsync_BothSentTheParameter_DeliversTheBytesBelowTheReliableSizeThenResetStreamAt()
    {
        using var key = RSA.Create(2048);
        using var client = new UnitTestFlowClient(UnitTestClientHelloEdits.ReplaceParameters(QuicStreamTests.ClientParametersOfferingResetStreamAt));
        var options = new QuicServerConnectionOptions { CertificateChain = [UnitTestCertificates.SelfSigned(key)], CertificateKey = key, ResetStreamAt = true };
        var protocol = new UnitTestApplicationProtocol(async (connection, token) =>
        {
            var stream = (await connection.AcceptStreamAsync(token))!;
            await connection.WriteAsync(stream, "headerbody"u8.ToArray(), fin: false, token);
            await connection.ResetAtAsync(stream, 7, 6, token);
            await connection.AcceptStreamAsync(CancellationToken.None);
        });

        var serving = client.Server(options, protocol).ServeAsync(client.Flow, TestContext.CancellationToken);
        await client.CompleteHandshakeAsync(TestContext.CancellationToken);
        client.SendOneRtt(new QuicStreamFrame(0, 0, "go"u8.ToArray(), Fin: false));
        await client.UntilAsync(() => client.OneRttFrames<QuicResetStreamAtFrame>().Any(), TestContext.CancellationToken);
        client.SendOneRtt(new QuicConnectionCloseFrame(true, 0, 0, ReadOnlyMemory<byte>.Empty));
        await client.FinishAsync(serving, TestContext.CancellationToken);

        Assert.AreEqual("2400070A06", Convert.ToHexString(client.OneRttFrames<QuicResetStreamAtFrame>().Single().ToBytes()), "Final Size 10, as all ten bytes went at once; Reliable Size 6.");
    }
}
