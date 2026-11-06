using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A <c>(</c> big number (RESP3): a signed integer of any size in decimal, e.g.
/// <c>(3492890328409238509324850943850943825024385</c>.
/// </summary>
/// <param name="value">The integer.</param>
public sealed class RespBigNumber(BigInteger value) : RespValue
{
    /// <summary>The integer.</summary>
    public BigInteger Value { get; } = value;

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) =>
        RespEncoding.WriteLine(output, (byte)'(', Encoding.ASCII.GetBytes(Value.ToString(CultureInfo.InvariantCulture)));
}
