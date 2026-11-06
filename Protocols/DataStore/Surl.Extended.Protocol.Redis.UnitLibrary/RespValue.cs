using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// One value of the Redis serialization protocol, RESP2 or RESP3 (the RESP page; RESP3.md), as a
/// reply the server writes. Each subclass is one type and writes its own prefix byte.
/// </summary>
/// <remarks>
/// A value is written exactly as constructed: choosing the RESP2 or the RESP3 form of a reply (a
/// null bulk string or a null, an array or a set) is the caller's, by the connection's protocol
/// version (ADR-0106 decision 2).
/// </remarks>
public abstract class RespValue
{
    private protected RespValue()
    {
    }

    /// <summary>Encodes the value as the bytes sent on the wire.</summary>
    /// <returns>The value's bytes, every line ended by CR LF.</returns>
    public byte[] Encode()
    {
        var output = new ArrayBufferWriter<byte>();
        EncodeTo(output);
        return output.WrittenSpan.ToArray();
    }

    /// <summary>Appends the value's bytes to <paramref name="output"/>.</summary>
    /// <param name="output">Where the bytes go.</param>
    internal abstract void EncodeTo(IBufferWriter<byte> output);
}
