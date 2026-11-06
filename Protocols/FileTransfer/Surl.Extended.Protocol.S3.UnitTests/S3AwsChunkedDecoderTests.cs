using System.Security.Cryptography;
using System.Text;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The <c>aws-chunked</c> framing of "Transferring Payload in Multiple Chunks" and its unsigned
/// trailer form (ADR-0108 decision 4.5), decoded a few bytes at a time and refused when malformed.
/// </summary>
[TestClass]
public sealed class S3AwsChunkedDecoderTests
{
    private const string Signature = "ad80c730a21e5b8d04586a2213dd63b9a0e99e0e2307b0ade35a65485a288648";

    private static async Task<(S3AwsChunkedDecoder Decoder, MemoryStream Sink)> DecodeAsync(string body, long decodedLength, UnitTestChunkSignatureChain? chain = null, int writeSize = int.MaxValue)
    {
        var sink = new MemoryStream();
        var decoder = new S3AwsChunkedDecoder(sink, chain, decodedLength);
        var bytes = Encoding.Latin1.GetBytes(body);
        for (var offset = 0; offset < bytes.Length; offset += writeSize)
        {
            await decoder.WriteAsync(bytes.AsMemory(offset, Math.Min(writeSize, bytes.Length - offset)), CancellationToken.None);
        }

        return (decoder, sink);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(int.MaxValue)]
    public async Task WriteAsync_UnsignedChunksAndTrailer_InAnySplits_DecodeTheDataAndReadTheTrailer(int writeSize)
    {
        var (decoder, sink) = await DecodeAsync("6\r\nhello \r\n5\r\nworld\r\n0\r\nx-amz-checksum-crc32 : DUoRhQ==\r\n\r\n", 11, writeSize: writeSize);

        Assert.IsNull(decoder.Finish());
        Assert.AreEqual("hello world", Encoding.Latin1.GetString(sink.ToArray()));
        Assert.AreEqual("DUoRhQ==", decoder.Trailer["X-AMZ-CHECKSUM-CRC32"]);
    }

    [TestMethod]
    public async Task WriteAsync_SignedChunks_AreCheckedInOrderOverTheirData()
    {
        var chain = new UnitTestChunkSignatureChain((SHA256.HashData("abc"u8), Signature), (SHA256.HashData([]), Signature));

        var (decoder, sink) = await DecodeAsync($"3;chunk-signature={Signature}\r\nabc\r\n0;chunk-signature={Signature}\r\n\r\n", 3, chain, writeSize: 2);

        Assert.IsNull(decoder.Finish());
        Assert.AreEqual("abc", Encoding.Latin1.GetString(sink.ToArray()));
        Assert.HasCount(2, chain.Asked);
    }

    [TestMethod]
    [DataRow("3\r\nabc\r\n0\r\n\r\n", DisplayName = "a chunk with no signature")]
    [DataRow("3;chunk-signature=" + Signature + "\r\nabc\r\n0;chunk-signature=" + Signature + "\r\nx-amz-checksum-crc32:AAAAAA==\r\n\r\n", DisplayName = "a trailer field after signed chunks")]
    public async Task WriteAsync_SignedChunksMalformed_IsIncompleteBody(string body)
    {
        var chain = new UnitTestChunkSignatureChain((SHA256.HashData("abc"u8), Signature), (SHA256.HashData([]), Signature));

        var (decoder, _) = await DecodeAsync(body, 3, chain);

        Assert.AreSame(S3Error.IncompleteBody, decoder.Finish());
    }

    [TestMethod]
    public async Task WriteAsync_ASignatureTheChainRefuses_IsSignatureDoesNotMatchAndTheRestDropped()
    {
        var chain = new UnitTestChunkSignatureChain((SHA256.HashData("abd"u8), Signature));

        var (decoder, sink) = await DecodeAsync($"3;chunk-signature={Signature}\r\nabc\r\n3;chunk-signature={Signature}\r\ndef\r\n", 6, chain);

        Assert.AreSame(S3Error.SignatureDoesNotMatch, decoder.Finish());
        Assert.AreEqual("abc", Encoding.Latin1.GetString(sink.ToArray()));
    }

    [TestMethod]
    [DataRow("zz\r\nabc\r\n0\r\n\r\n", 3, DisplayName = "a size that is not hex")]
    [DataRow("\r\n", 0, DisplayName = "an empty chunk header")]
    [DataRow("3;chunk-signature=" + Signature + "\r\nabc\r\n0\r\n\r\n", 3, DisplayName = "a signature on an unsigned chunk")]
    [DataRow("3\nabc\r\n0\r\n\r\n", 3, DisplayName = "a bare line feed")]
    [DataRow("3\r\nabcX\r\n0\r\n\r\n", 3, DisplayName = "data past the chunk's size")]
    [DataRow("4\r\nabcd\r\n0\r\n\r\n", 3, DisplayName = "data past the decoded length")]
    [DataRow("FFFFFFFFFFFFFFFF\r\n", 3, DisplayName = "a size that overflows")]
    [DataRow("0\r\nno colon\r\n\r\n", 0, DisplayName = "a trailer line without a colon")]
    [DataRow("0\r\n:value\r\n\r\n", 0, DisplayName = "a trailer line without a name")]
    [DataRow("0\r\n\r\nmore", 0, DisplayName = "bytes after the body")]
    [DataRow("3\r\nabc\r\n0\r\n", 3, DisplayName = "no blank line after the trailer")]
    [DataRow("3\r\nab", 3, DisplayName = "a chunk cut short")]
    [DataRow("2\r\nab\r\n0\r\n\r\n", 3, DisplayName = "data short of the decoded length")]
    public async Task WriteAsync_Malformed_IsIncompleteBody(string body, long decodedLength)
    {
        var (decoder, _) = await DecodeAsync(body, decodedLength, writeSize: 1);

        Assert.AreSame(S3Error.IncompleteBody, decoder.Finish());
    }

    [TestMethod]
    public async Task WriteAsync_ALinePastTheLongestRead_IsIncompleteBody()
    {
        var (decoder, _) = await DecodeAsync(new string('1', S3AwsChunkedDecoder.MaxLineLength + 1), 1);

        Assert.AreSame(S3Error.IncompleteBody, decoder.Finish());
    }

    [TestMethod]
    public async Task WriteAsync_ArrayOverload_DecodesLikeTheMemoryOne()
    {
        var sink = new MemoryStream();
        await using var decoder = new S3AwsChunkedDecoder(sink, null, 2);
        var bytes = "2\r\nhi\r\n0\r\n\r\n"u8.ToArray();

        await decoder.WriteAsync(bytes, 0, bytes.Length, CancellationToken.None);

        Assert.IsNull(decoder.Finish());
        Assert.AreEqual("hi", Encoding.Latin1.GetString(sink.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsync_ASinkThatCompletesLater_DecodesTheSame()
    {
        var sink = new YieldingStream();
        await using var decoder = new S3AwsChunkedDecoder(sink, null, 2);

        await decoder.WriteAsync("2\r\nhi\r\n0\r\n\r\n"u8.ToArray(), CancellationToken.None);

        Assert.IsNull(decoder.Finish());
        Assert.AreEqual("hi", Encoding.Latin1.GetString(sink.ToArray()));
    }

    [TestMethod]
    public void Stream_EverythingButAnAsynchronousWrite_IsNotSupported()
    {
        using var stream = new S3AwsChunkedDecoder(Stream.Null, null, 0);

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        stream.Flush();
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    // A sink whose every write completes only after yielding, as a store on a slow disk does.
    private sealed class YieldingStream : MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            await base.WriteAsync(buffer, cancellationToken);
        }
    }
}
