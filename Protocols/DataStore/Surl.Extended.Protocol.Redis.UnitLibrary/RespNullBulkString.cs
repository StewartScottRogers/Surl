using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The <c>$-1</c> null bulk string, RESP2's missing value.
/// </summary>
public sealed class RespNullBulkString : RespValue
{
    private RespNullBulkString()
    {
    }

    /// <summary>The one null bulk string.</summary>
    public static RespNullBulkString Instance { get; } = new();

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) => output.Write("$-1\r\n"u8);
}
