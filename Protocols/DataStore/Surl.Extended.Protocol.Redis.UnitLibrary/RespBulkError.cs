using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A <c>!</c> bulk error (RESP3): an error sent with its length, its first word the error code, e.g. <c>!21 SYNTAX invalid syntax</c>.
/// </summary>
public sealed class RespBulkError : RespValue
{
    private readonly byte[] bytes;

    /// <summary>Creates a bulk error of <paramref name="text"/>'s UTF-8 bytes.</summary>
    /// <param name="text">The text.</param>
    public RespBulkError(string text)
        : this(RespEncoding.Utf8(text, nameof(text)))
    {
    }

    /// <summary>Creates a bulk error of <paramref name="bytes"/>.</summary>
    /// <param name="bytes">The bytes, any byte allowed.</param>
    public RespBulkError(ReadOnlySpan<byte> bytes)
    {
        this.bytes = bytes.ToArray();
    }

    /// <summary>The bytes sent after the length line.</summary>
    public ReadOnlyMemory<byte> Bytes => bytes;

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) => RespEncoding.WriteBlob(output, (byte)'!', bytes);
}
