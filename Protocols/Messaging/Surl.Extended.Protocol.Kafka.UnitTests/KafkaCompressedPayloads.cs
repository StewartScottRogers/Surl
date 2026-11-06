using System.IO.Compression;
using Surl.Compression;
using static Surl.Extended.Protocol.Kafka.UnitTests.KafkaTestExchanges;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// Compressed record payloads written from each codec's format document, never from the Curl
/// port: gzip through the BCL's <see cref="GZipStream"/> (RFC 1952); raw Snappy of literals only
/// and its xerial framing (google/snappy <c>format_description.txt</c>, snappy-java); one LZ4
/// frame of uncompressed blocks (lz4 <c>doc/lz4_Frame_format.md</c>); one zstd frame of a
/// Raw_Block (RFC 8878 section 3.1.1).
/// </summary>
internal static class KafkaCompressedPayloads
{
    /// <summary>The codecs 1 to 4 with a payload builder each, for data rows.</summary>
    public static IEnumerable<object[]> Codecs =>
    [
        [(short)1, "gzip"],
        [(short)2, "snappy"],
        [(short)2, "xerial"],
        [(short)3, "lz4"],
        [(short)4, "zstd"],
    ];

    /// <summary>The served codecs 0 to 4, for data rows.</summary>
    public static IEnumerable<object[]> ServedCodecs =>
    [
        [KafkaCompressionCodec.None],
        [KafkaCompressionCodec.Gzip],
        [KafkaCompressionCodec.Snappy],
        [KafkaCompressionCodec.Lz4],
        [KafkaCompressionCodec.Zstd],
    ];

    /// <summary><paramref name="bytes"/> compressed by the builder <paramref name="format"/> names.</summary>
    public static byte[] Compress(string format, byte[] bytes) => format switch
    {
        "gzip" => Gzip(bytes),
        "snappy" => RawSnappy(bytes),
        "xerial" => XerialSnappy(bytes),
        "lz4" => Lz4Frame(bytes),
        _ => ZstdFrame(bytes),
    };

    /// <summary>A gzip member written by <see cref="GZipStream"/>.</summary>
    public static byte[] Gzip(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(bytes);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Raw Snappy: the uncompressed length as a little-endian varint, then literals of at most 60
    /// bytes, each tag <c>(length - 1) &lt;&lt; 2</c> with element type 00.
    /// </summary>
    public static byte[] RawSnappy(byte[] bytes)
    {
        var output = new List<byte>();
        var length = (uint)bytes.Length;
        while (length >= 0x80)
        {
            output.Add((byte)(length | 0x80));
            length >>= 7;
        }

        output.Add((byte)length);
        foreach (var chunk in bytes.Chunk(60))
        {
            output.Add((byte)((chunk.Length - 1) << 2));
            output.AddRange(chunk);
        }

        return [.. output];
    }

    /// <summary>
    /// snappy-java's xerial framing: <c>0x82 "SNAPPY" 0x00</c>, version 1, compatible version 1,
    /// then one chunk, a big-endian INT32 length and raw Snappy.
    /// </summary>
    public static byte[] XerialSnappy(byte[] bytes)
    {
        var chunk = RawSnappy(bytes);
        return [0x82, .. "SNAPPY"u8, 0x00, .. Int32(1), .. Int32(1), .. Int32(chunk.Length), .. chunk];
    }

    /// <summary>
    /// One LZ4 frame: magic <c>04 22 4D 18</c>, FLG <c>0x60</c> (version 01, independent blocks,
    /// no checksums, no content size), BD <c>0x40</c> (64 KiB blocks), header checksum
    /// <c>0x82</c> (the second byte of XXH32 of FLG and BD), uncompressed blocks (size with the
    /// high bit set), then the end mark.
    /// </summary>
    public static byte[] Lz4Frame(byte[] bytes)
    {
        var output = new List<byte> { 0x04, 0x22, 0x4D, 0x18, 0x60, 0x40, 0x82 };
        foreach (var block in bytes.Chunk(64 * 1024))
        {
            output.AddRange(LittleEndian32((uint)block.Length | 0x80000000u));
            output.AddRange(block);
        }

        output.AddRange(LittleEndian32(0));
        return [.. output];
    }

    /// <summary>
    /// One zstd frame: magic <c>28 B5 2F FD</c>, a Frame_Header_Descriptor with
    /// Single_Segment_flag - <c>0x20</c> and a 1-byte Frame_Content_Size below 256 bytes,
    /// <c>0x60</c> and a 2-byte one stored less 256 from 256 to 65791 - then one last Raw_Block:
    /// a 3-byte little-endian header <c>Block_Size &lt;&lt; 3 | 1</c> and the bytes.
    /// </summary>
    public static byte[] ZstdFrame(byte[] bytes)
    {
        byte[] header = bytes.Length < 256
            ? [0x20, (byte)bytes.Length]
            : [0x60, (byte)(bytes.Length - 256), (byte)((bytes.Length - 256) >> 8)];
        var blockHeader = (bytes.Length << 3) | 1;
        return
        [
            0x28, 0xB5, 0x2F, 0xFD, .. header,
            (byte)blockHeader, (byte)(blockHeader >> 8), (byte)(blockHeader >> 16),
            .. bytes,
        ];
    }

    /// <summary>
    /// Decompresses a served batch's payload under <paramref name="codec"/>: gzip with
    /// <see cref="GZipStream"/>, the others with <c>Surl.Compression</c>'s decoders, which their
    /// own tests pin to each format's published vectors.
    /// </summary>
    public static byte[] Decompress(KafkaCompressionCodec codec, byte[] payload)
    {
        switch (codec)
        {
            case KafkaCompressionCodec.None:
                return payload;
            case KafkaCompressionCodec.Gzip:
                using (var gzip = new GZipStream(new MemoryStream(payload), CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    return output.ToArray();
                }

            case KafkaCompressionCodec.Snappy:
                Assert.IsTrue(XerialSnappyFraming.HasMagic(payload), "served Snappy is xerial-framed");
                return Decoded(XerialSnappyFraming.DecodeFramedOrRaw(payload, int.MaxValue));
            case KafkaCompressionCodec.Lz4:
                return Decoded(Lz4FrameDecoder.Decode(payload, int.MaxValue));
            default:
                return Decoded(ZstdFrameDecoder.Decode(payload, int.MaxValue));
        }
    }

    private static byte[] Decoded(DecompressionResult result)
    {
        Assert.AreEqual(DecompressionOutcome.Decompressed, result.Outcome, result.Reason);
        return result.Output;
    }

    private static byte[] LittleEndian32(uint value) => [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];
}
