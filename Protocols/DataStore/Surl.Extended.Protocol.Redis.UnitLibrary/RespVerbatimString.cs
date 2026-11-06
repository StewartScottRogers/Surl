using System.Buffers;
using System.Text;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A <c>=</c> verbatim string (RESP3): a bulk string whose first four bytes are a three-byte
/// format and a colon, e.g. <c>=15 txt:Some string</c>.
/// </summary>
public sealed class RespVerbatimString : RespValue
{
    private readonly byte[] text;

    /// <summary>Creates a verbatim string of <paramref name="text"/>'s UTF-8 bytes.</summary>
    /// <param name="format">Exactly three printable ASCII characters other than a colon, e.g. <c>txt</c> or <c>mkd</c>.</param>
    /// <param name="text">The text.</param>
    /// <exception cref="ArgumentException"><paramref name="format"/> is not three such characters.</exception>
    public RespVerbatimString(string format, string text)
        : this(format, RespEncoding.Utf8(text, nameof(text)))
    {
    }

    /// <summary>Creates a verbatim string of <paramref name="text"/>.</summary>
    /// <param name="format">Exactly three printable ASCII characters other than a colon, e.g. <c>txt</c> or <c>mkd</c>.</param>
    /// <param name="text">The bytes, any byte allowed.</param>
    /// <exception cref="ArgumentException"><paramref name="format"/> is not three such characters.</exception>
    public RespVerbatimString(string format, ReadOnlySpan<byte> text)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (format.Length != 3 || !format.All(IsFormatCharacter))
        {
            throw new ArgumentException("The format is three printable ASCII characters other than a colon.", nameof(format));
        }

        Format = format;
        this.text = text.ToArray();
    }

    /// <summary>The three-character format.</summary>
    public string Format { get; }

    /// <summary>The bytes after the format and its colon.</summary>
    public ReadOnlyMemory<byte> Text => text;

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) =>
        RespEncoding.WriteBlob(output, (byte)'=', [.. Encoding.ASCII.GetBytes(Format), (byte)':', .. text]);

    private static bool IsFormatCharacter(char character) => character is > ' ' and <= '~' and not ':';
}
