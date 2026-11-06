using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// The <c>_</c> null, RESP3's one missing value.
/// </summary>
public sealed class RespNull : RespValue
{
    private RespNull()
    {
    }

    /// <summary>The one null.</summary>
    public static RespNull Instance { get; } = new();

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) => output.Write("_\r\n"u8);
}
