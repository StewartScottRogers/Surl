using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A <c>:</c> integer (RESP2 and RESP3): a signed 64-bit integer in decimal, e.g. <c>:1000</c>.
/// </summary>
/// <param name="value">The integer.</param>
public sealed class RespInteger(long value) : RespValue
{
    /// <summary>The integer.</summary>
    public long Value { get; } = value;

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) => RespEncoding.WriteLine(output, (byte)':', Value);
}
