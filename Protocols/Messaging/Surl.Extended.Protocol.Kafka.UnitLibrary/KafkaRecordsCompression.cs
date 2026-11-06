using System.IO.Compression;
using Surl.Compression;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Compresses and decompresses the records of one record batch under its codec (ADR-0105
/// decision 4): gzip through the BCL's <see cref="GZipStream"/>, Snappy (raw or in xerial
/// framing), LZ4 and zstd through <c>Surl.Compression</c>.
/// </summary>
public static class KafkaRecordsCompression
{
    /// <summary>
    /// The decompression bound when <c>--max-message</c> is 0: 268435456 bytes (256 MiB).
    /// </summary>
    public const int BoundWithoutMaxMessage = 256 * 1024 * 1024;

    /// <summary>How many times <c>--max-message</c> one batch's records may decompress to: 4.</summary>
    public const int MaxMessageMultiple = 4;

    private const int GzipReadBufferBytes = 64 * 1024;

    /// <summary>
    /// The most bytes one batch's records may decompress to: <paramref name="maxMessageBytes"/>
    /// times <see cref="MaxMessageMultiple"/>, or <see cref="BoundWithoutMaxMessage"/> when it is
    /// 0, and never more than the largest array.
    /// </summary>
    /// <param name="maxMessageBytes"><c>--max-message</c>, 0 for no limit.</param>
    /// <returns>The bound, in bytes.</returns>
    public static int DecompressionBound(long maxMessageBytes) =>
        maxMessageBytes <= 0 ? BoundWithoutMaxMessage
        : (int)Math.Min(maxMessageBytes * MaxMessageMultiple, Array.MaxLength);

    /// <summary>
    /// Decompresses <paramref name="payload"/> under <paramref name="codec"/>, writing at most
    /// <paramref name="bound"/> bytes.
    /// </summary>
    /// <param name="codec">The batch's codec, 1 to 4.</param>
    /// <param name="payload">The bytes after the batch header.</param>
    /// <param name="bound">The most bytes the records may decompress to.</param>
    /// <param name="records">The decompressed records; empty unless the answer is <see cref="KafkaErrorCode.None"/>.</param>
    /// <returns>
    /// <see cref="KafkaErrorCode.None"/>; <see cref="KafkaErrorCode.MessageTooLarge"/> past
    /// <paramref name="bound"/>; <see cref="KafkaErrorCode.CorruptMessage"/> for a payload its
    /// codec cannot decode, a checksum that does not hold, or a codec that is not 1 to 4.
    /// </returns>
    public static KafkaErrorCode Decompress(KafkaCompressionCodec codec, ReadOnlySpan<byte> payload, int bound, out byte[] records)
    {
        var result = codec switch
        {
            KafkaCompressionCodec.Gzip => DecompressGzip(payload, bound),
            KafkaCompressionCodec.Snappy => FromResult(XerialSnappyFraming.DecodeFramedOrRaw(payload, bound)),
            KafkaCompressionCodec.Lz4 => FromResult(Lz4FrameDecoder.Decode(payload, bound)),
            KafkaCompressionCodec.Zstd => FromResult(ZstdFrameDecoder.Decode(payload, bound)),
            _ => (KafkaErrorCode.CorruptMessage, []),
        };
        records = result.Records;
        return result.Error;
    }

    /// <summary>
    /// Compresses <paramref name="records"/> under <paramref name="codec"/> with the encoders
    /// ADR-0105 decision 4 names: gzip at <see cref="CompressionLevel.Optimal"/>, Snappy in xerial
    /// framing, one LZ4 frame, one zstd frame; <see cref="KafkaCompressionCodec.None"/> returns
    /// the records as they are.
    /// </summary>
    /// <param name="codec">The codec.</param>
    /// <param name="records">The records' bytes.</param>
    /// <returns>The compressed bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="codec"/> is not 0 to 4.</exception>
    public static byte[] Compress(KafkaCompressionCodec codec, ReadOnlySpan<byte> records) => codec switch
    {
        KafkaCompressionCodec.None => records.ToArray(),
        KafkaCompressionCodec.Gzip => CompressGzip(records),
        KafkaCompressionCodec.Snappy => XerialSnappyFraming.Encode(records),
        KafkaCompressionCodec.Lz4 => Lz4FrameEncoder.Encode(records),
        KafkaCompressionCodec.Zstd => ZstdFrameEncoder.Encode(records),
        _ => throw new ArgumentOutOfRangeException(nameof(codec), codec, "The codec is not 0 to 4."),
    };

    private static (KafkaErrorCode Error, byte[] Records) FromResult(DecompressionResult result) => result.Outcome switch
    {
        DecompressionOutcome.Decompressed => (KafkaErrorCode.None, result.Output),
        DecompressionOutcome.OutputBoundPassed => (KafkaErrorCode.MessageTooLarge, []),
        _ => (KafkaErrorCode.CorruptMessage, []),
    };

    private static (KafkaErrorCode Error, byte[] Records) DecompressGzip(ReadOnlySpan<byte> payload, int bound)
    {
        using var gzip = new GZipStream(new MemoryStream(payload.ToArray()), CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[GzipReadBufferBytes];
        try
        {
            int read;
            while ((read = gzip.Read(buffer)) > 0)
            {
                if (output.Length + read > bound)
                {
                    return (KafkaErrorCode.MessageTooLarge, []);
                }

                output.Write(buffer, 0, read);
            }
        }
        catch (InvalidDataException)
        {
            return (KafkaErrorCode.CorruptMessage, []);
        }

        return (KafkaErrorCode.None, output.ToArray());
    }

    private static byte[] CompressGzip(ReadOnlySpan<byte> records)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(records);
        }

        return output.ToArray();
    }
}
