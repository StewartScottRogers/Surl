using System.Buffers.Binary;

namespace Surl.Compression;

/// <summary>
/// snappy-java's xerial stream framing, which Kafka's Java producer writes for the Snappy codec:
/// the magic <c>0x82 "SNAPPY" 0x00</c>, a big-endian INT32 version and compatible version, then
/// chunks, each a big-endian INT32 length and that many bytes of raw Snappy.
/// </summary>
public static class XerialSnappyFraming
{
    /// <summary>How much input each chunk the encoder writes holds: 32 KiB, as Kafka's producer writes.</summary>
    public const int ChunkInputLength = 32 * 1024;

    private const int HeaderLength = 16;

    private const int Version = 1;

    private const int CompatibleVersion = 1;

    private static ReadOnlySpan<byte> Magic => [0x82, (byte)'S', (byte)'N', (byte)'A', (byte)'P', (byte)'P', (byte)'Y', 0x00];

    /// <summary>Whether <paramref name="input"/> starts with the xerial magic.</summary>
    public static bool HasMagic(ReadOnlySpan<byte> input) => input.StartsWith(Magic);

    /// <summary>
    /// Decodes <paramref name="input"/> as xerial-framed Snappy when it starts with the magic, and
    /// as raw Snappy (<see cref="SnappyDecoder.Decode"/>) otherwise, writing at most
    /// <paramref name="outputBound"/> bytes across all its chunks.
    /// </summary>
    public static DecompressionResult DecodeFramedOrRaw(ReadOnlySpan<byte> input, int outputBound)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(outputBound);
        if (!HasMagic(input))
        {
            return SnappyDecoder.Decode(input, outputBound);
        }

        try
        {
            return DecompressionResult.Decompressed(DecodeFramedOrRefuse(input, outputBound));
        }
        catch (DecompressionRefusedException refusal)
        {
            return refusal.ToResult();
        }
    }

    /// <summary>
    /// <paramref name="input"/> in xerial framing: the header, version 1 and compatible version 1,
    /// then one chunk of raw Snappy (<see cref="SnappyEncoder"/>) per <see cref="ChunkInputLength"/>
    /// bytes of input.
    /// </summary>
    public static byte[] Encode(ReadOnlySpan<byte> input)
    {
        List<byte> output = [.. Magic];
        AppendInt32BigEndian(output, Version);
        AppendInt32BigEndian(output, CompatibleVersion);
        for (int start = 0; start < input.Length; start += ChunkInputLength)
        {
            byte[] chunk = SnappyEncoder.Encode(input.Slice(start, Math.Min(ChunkInputLength, input.Length - start)));
            AppendInt32BigEndian(output, chunk.Length);
            output.AddRange(chunk);
        }

        return [.. output];
    }

    private static byte[] DecodeFramedOrRefuse(ReadOnlySpan<byte> input, int outputBound)
    {
        if (input.Length < HeaderLength)
        {
            throw DecompressionRefusedException.Malformed("the xerial header is truncated");
        }

        BoundedDecompressionOutput output = new(
            outputBound, DecompressionOutcome.OutputBoundPassed, "the chunks pass the output bound");
        ReadOnlySpan<byte> rest = input[HeaderLength..];
        while (!rest.IsEmpty)
        {
            int length = rest.Length < sizeof(int) ? -1 : BinaryPrimitives.ReadInt32BigEndian(rest);
            if (length < 0 || length > rest.Length - sizeof(int))
            {
                throw DecompressionRefusedException.Malformed("a xerial chunk is truncated");
            }

            output.Append(SnappyDecoder.DecodeOrRefuse(rest.Slice(sizeof(int), length), outputBound - output.Count));
            rest = rest[(sizeof(int) + length)..];
        }

        return output.ToArray();
    }

    private static void AppendInt32BigEndian(List<byte> output, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        output.AddRange(bytes);
    }
}
