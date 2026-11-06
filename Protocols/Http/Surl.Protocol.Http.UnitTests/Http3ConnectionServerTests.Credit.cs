using System.Security.Cryptography;
using Surl.Quic;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// A content-store file larger than the client's credit goes out only as the client grants more
/// (BL-710, ADR-0082 amendment 3): the server reads the file no further past the client's credit
/// than <see cref="QuicApplicationConnection.MaxUnsentLength"/> and the one copy buffer the store
/// is writing.
/// </summary>
public sealed partial class Http3ConnectionServerTests
{
    /// <summary>The buffer <c>ContentStore.CopyFileBytesAsync</c> reads a file through: 81920 bytes.</summary>
    private const int ContentStoreCopyBufferSize = 81920;

    [TestMethod]
    public async Task ServeAsync_FileLargerThanTheClientsStreamCredit_IsSentWholeOnlyAsTheClientGrantsCredit()
    {
        var body = RandomNumberGenerator.GetBytes(256 * 1024);
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "large.bin"), body, FileTime);
        using var client = new Http3TestClient();
        await client.StartAsync(Token, fileSystem: fileSystem);
        client.Flow.DeliverFromClient(client.Quic.SealOneRtt(new QuicMaxDataFrame(1UL << 40)));

        client.Send(0, client.Request("GET", "/large.bin"), fin: true);
        var grants = 0;
        var grantedAt = -1;
        var acknowledged = 0;
        while (!client.StreamSent(0).Fin)
        {
            await client.UntilAsync(() => client.StreamSent(0).Fin || IsBlockedAtItsCredit(client, grantedAt) || client.Quic.PacketNumbersReceived[QuicPacketType.OneRtt].Count > acknowledged, Token);
            acknowledged = client.Quic.PacketNumbersReceived[QuicPacketType.OneRtt].Count;
            client.Flow.DeliverFromClient(client.Quic.SealOneRtt(client.Quic.AckFrame(QuicPacketType.OneRtt)));
            if (IsBlockedAtItsCredit(client, grantedAt))
            {
                // Until the grant below, every byte the credit allows is sent, so the file is read at most the bound and one copy buffer past it.
                var read = fileSystem.OpenedFiles.Single() switch { { CanRead: true } file => file.Position, _ => body.Length };
                Assert.IsLessThanOrEqualTo((long)client.StreamSent(0).Data.Length + QuicApplicationConnection.MaxUnsentLength + ContentStoreCopyBufferSize, read, "The file was read further past the client's credit than the bound and one copy buffer.");
                grants++;
                grantedAt = client.StreamSent(0).Data.Length;
                client.Flow.DeliverFromClient(client.Quic.SealOneRtt(new QuicMaxStreamDataFrame(0, (ulong)client.StreamSent(0).Data.Length + 32768)));
            }
        }

        var response = client.Response(0);
        Assert.AreEqual("200", response.Status);
        CollectionAssert.AreEqual(body, response.Body.ToArray());
        Assert.IsGreaterThanOrEqualTo(5, grants, "256 KiB in 32 KiB grants after the client's first window.");
    }

    /// <summary>Whether the server has sent all the credit stream 0 has, said so with <c>STREAM_DATA_BLOCKED</c>, and was not yet granted more there.</summary>
    private static bool IsBlockedAtItsCredit(Http3TestClient client, long grantedAt)
    {
        var sent = (ulong)client.StreamSent(0).Data.Length;
        return (long)sent != grantedAt && client.OneRttFrames<QuicStreamDataBlockedFrame>().Any(frame => frame.StreamId == 0 && frame.MaximumStreamData == sent);
    }
}
