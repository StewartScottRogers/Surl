using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The <c>*-1</c> null array, RESP2's missing array.
/// </summary>
public sealed class RespNullArray : RespValue
{
    private RespNullArray()
    {
    }

    /// <summary>The one null array.</summary>
    public static RespNullArray Instance { get; } = new();

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) => output.Write("*-1\r\n"u8);
}
