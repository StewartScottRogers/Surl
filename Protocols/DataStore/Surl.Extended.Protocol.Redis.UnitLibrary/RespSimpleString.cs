using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A <c>+</c> simple string (RESP2 and RESP3), e.g. <c>+OK</c>. It cannot hold CR or LF (the RESP page).
/// </summary>
public sealed class RespSimpleString : RespValue
{
    private readonly byte[] text;

    /// <summary>Creates a simple string of <paramref name="text"/>'s UTF-8 bytes.</summary>
    /// <param name="text">The text, without CR or LF.</param>
    /// <exception cref="ArgumentException"><paramref name="text"/> holds CR or LF.</exception>
    public RespSimpleString(string text)
        : this(RespEncoding.Utf8(text, nameof(text)))
    {
    }

    /// <summary>Creates a simple string of <paramref name="text"/>'s bytes.</summary>
    /// <param name="text">The bytes, without CR or LF.</param>
    /// <exception cref="ArgumentException"><paramref name="text"/> holds CR or LF.</exception>
    public RespSimpleString(ReadOnlySpan<byte> text)
    {
        this.text = RespEncoding.RequireNoLineBreak(text, nameof(text));
    }

    /// <summary>The bytes between the prefix and CR LF.</summary>
    public ReadOnlyMemory<byte> Text => text;

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) => RespEncoding.WriteLine(output, (byte)'+', text);
}
