using System.Buffers;
using System.Globalization;
using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A <c>,</c> double (RESP3): the shortest decimal that reads back as the same double, e.g.
/// <c>,1.23</c> or <c>,1e+20</c>, and <c>inf</c>, <c>-inf</c> and <c>nan</c> for the values
/// without one.
/// </summary>
/// <param name="value">The double.</param>
public sealed class RespDouble(double value) : RespValue
{
    /// <summary>The double.</summary>
    public double Value { get; } = value;

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) =>
        RespEncoding.WriteLine(output, (byte)',', Encoding.ASCII.GetBytes(FormatDouble(Value)));

    private static string FormatDouble(double value) => value switch
    {
        double.PositiveInfinity => "inf",
        double.NegativeInfinity => "-inf",
        double.NaN => "nan",
        _ => value.ToString("R", CultureInfo.InvariantCulture).Replace('E', 'e'),
    };
}
