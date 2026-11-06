using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A <c>#</c> boolean (RESP3): <c>#t</c> or <c>#f</c>.
/// </summary>
public sealed class RespBoolean : RespValue
{
    private RespBoolean(bool value)
    {
        Value = value;
    }

    /// <summary>The boolean <c>#t</c>.</summary>
    public static RespBoolean True { get; } = new(true);

    /// <summary>The boolean <c>#f</c>.</summary>
    public static RespBoolean False { get; } = new(false);

    /// <summary>The boolean's value.</summary>
    public bool Value { get; }

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) =>
        RespEncoding.WriteLine(output, (byte)'#', Value ? "t"u8 : "f"u8);
}
