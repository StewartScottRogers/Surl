using System.IO.Compression;

namespace Surl.Extended.Protocol.Grpc;

[TestClass]
public sealed class GrpcMessageDecompressionTests
{
    private static readonly byte[] Serialized = [0x0A, 0x05, 0x68, 0x65, 0x6C, 0x6C, 0x6F];

    [TestMethod]
    [DataRow(null, GrpcMessageEncoding.Identity)]
    [DataRow("identity", GrpcMessageEncoding.Identity)]
    [DataRow("gzip", GrpcMessageEncoding.Gzip)]
    [DataRow("GZip", GrpcMessageEncoding.Gzip)]
    [DataRow("deflate", GrpcMessageEncoding.Deflate)]
    public void ReadEncoding_ServedEncoding_ReturnsIt(string? grpcEncoding, GrpcMessageEncoding expected)
    {
        Assert.AreEqual(expected, GrpcMessageDecompression.ReadEncoding(grpcEncoding));
    }

    [TestMethod]
    [DataRow("br")]
    [DataRow("snappy")]
    [DataRow("")]
    public void ReadEncoding_OtherEncoding_ThrowsUnimplemented(string grpcEncoding)
    {
        var thrown = Assert.ThrowsExactly<GrpcCallException>(() => GrpcMessageDecompression.ReadEncoding(grpcEncoding));

        Assert.AreEqual(GrpcStatusCode.Unimplemented, thrown.Status.Code);
        Assert.AreEqual("grpc-encoding not served", thrown.Status.Message);
    }

    [TestMethod]
    public void AcceptEncodingValue_IsWhatADR0116Decision6Advertises()
    {
        Assert.AreEqual("identity,deflate,gzip", GrpcMessageDecompression.AcceptEncodingValue);
    }

    [TestMethod]
    public void ReadPayload_GzipFlaggedMessage_IsDecompressed()
    {
        var message = new LengthPrefixedMessage(true, Compress(Serialized, stream => new GZipStream(stream, CompressionLevel.Optimal)));

        var payload = GrpcMessageDecompression.ReadPayload(message, GrpcMessageEncoding.Gzip, 1048576);

        CollectionAssert.AreEqual(Serialized, payload.ToArray());
    }

    [TestMethod]
    public void ReadPayload_DeflateFlaggedMessage_IsDecompressedFromTheZlibFormat()
    {
        var compressed = Compress(Serialized, stream => new ZLibStream(stream, CompressionLevel.Optimal));
        var message = new LengthPrefixedMessage(true, compressed);

        var payload = GrpcMessageDecompression.ReadPayload(message, GrpcMessageEncoding.Deflate, 1048576);

        Assert.AreEqual(0x78, compressed[0]);
        CollectionAssert.AreEqual(Serialized, payload.ToArray());
    }

    [TestMethod]
    public void ReadPayload_UncompressedMessageUnderGzip_IsReadAsIs()
    {
        var message = new LengthPrefixedMessage(false, Serialized);

        var payload = GrpcMessageDecompression.ReadPayload(message, GrpcMessageEncoding.Gzip, 1048576);

        CollectionAssert.AreEqual(Serialized, payload.ToArray());
    }

    [TestMethod]
    public void ReadPayload_CompressedFlagUnderIdentity_ThrowsInternal()
    {
        var message = new LengthPrefixedMessage(true, Serialized);

        var thrown = Assert.ThrowsExactly<GrpcCallException>(() => GrpcMessageDecompression.ReadPayload(message, GrpcMessageEncoding.Identity, 1048576));

        Assert.AreEqual(GrpcStatusCode.Internal, thrown.Status.Code);
        Assert.AreEqual("compressed flag set without grpc-encoding", thrown.Status.Message);
    }

    [TestMethod]
    [DataRow(GrpcMessageEncoding.Gzip)]
    [DataRow(GrpcMessageEncoding.Deflate)]
    public void ReadPayload_BytesThatDoNotDecompress_ThrowsInternal(GrpcMessageEncoding encoding)
    {
        var message = new LengthPrefixedMessage(true, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });

        var thrown = Assert.ThrowsExactly<GrpcCallException>(() => GrpcMessageDecompression.ReadPayload(message, encoding, 1048576));

        Assert.AreEqual(GrpcStatus.MessageDidNotDecompress, thrown.Status);
        Assert.IsInstanceOfType<InvalidDataException>(thrown.InnerException);
    }

    [TestMethod]
    public void ReadPayload_DecompressedPastTheLimit_ThrowsResourceExhausted()
    {
        var large = new byte[200000];
        var message = new LengthPrefixedMessage(true, Compress(large, stream => new GZipStream(stream, CompressionLevel.Optimal)));

        var thrown = Assert.ThrowsExactly<GrpcCallException>(() => GrpcMessageDecompression.ReadPayload(message, GrpcMessageEncoding.Gzip, 100000));

        Assert.AreEqual(GrpcStatus.MessageLargerThanTheLimit, thrown.Status);
    }

    [TestMethod]
    public void ReadPayload_DecompressedToExactlyTheLimit_IsRead()
    {
        var message = new LengthPrefixedMessage(true, Compress(Serialized, stream => new GZipStream(stream, CompressionLevel.Optimal)));

        var payload = GrpcMessageDecompression.ReadPayload(message, GrpcMessageEncoding.Gzip, Serialized.Length);

        Assert.AreEqual(Serialized.Length, payload.Length);
    }

    [TestMethod]
    public void ReadPayload_NullMessage_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => GrpcMessageDecompression.ReadPayload(null!, GrpcMessageEncoding.Gzip, 1));
    }

    private static byte[] Compress(byte[] bytes, Func<Stream, Stream> compressor)
    {
        using var output = new MemoryStream();
        using (var compressing = compressor(output))
        {
            compressing.Write(bytes);
        }

        return output.ToArray();
    }
}
