using System.Globalization;
using System.Text;

namespace Surl.Protocol.Imap;

/// <summary>
/// Builds the bytes of a response that carries message data (ADR-0055, decision 4): fixed ASCII
/// text, strings quoted when they hold only printable ASCII and sent as literals otherwise, and
/// literals, so no byte of a message ever reaches the response outside a literal or a quoted
/// string it cannot break out of.
/// </summary>
internal sealed class ImapDataWriter
{
    private readonly MemoryStream bytes = new();

    /// <summary>
    /// Writes fixed text: printable ASCII the server made, never a byte the peer or a message
    /// chose.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>This writer.</returns>
    public ImapDataWriter Text(string text)
    {
        bytes.Write(Encoding.ASCII.GetBytes(text));
        return this;
    }

    /// <summary>
    /// Writes a number in decimal.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>This writer.</returns>
    public ImapDataWriter Number(long value) => Text(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Writes a string: quoted, with <c>\</c> before each <c>"</c> and <c>\</c>, when every byte
    /// is printable ASCII; otherwise a literal.
    /// </summary>
    /// <param name="value">The string's bytes.</param>
    /// <returns>This writer.</returns>
    public ImapDataWriter String(ReadOnlySpan<byte> value)
    {
        if (value.ContainsAnyExceptInRange((byte)0x20, (byte)0x7E))
        {
            return Literal(value);
        }

        bytes.WriteByte((byte)'"');
        foreach (var next in value)
        {
            if (next is (byte)'"' or (byte)'\\')
            {
                bytes.WriteByte((byte)'\\');
            }

            bytes.WriteByte(next);
        }

        bytes.WriteByte((byte)'"');
        return this;
    }

    /// <summary>
    /// Writes a string as <see cref="String(ReadOnlySpan{byte})"/> does, from its UTF-8 bytes.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <returns>This writer.</returns>
    public ImapDataWriter String(string value) => String(Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Writes a string, or <c>NIL</c> when there is none.
    /// </summary>
    /// <param name="value">The string, or <see langword="null"/>.</param>
    /// <returns>This writer.</returns>
    public ImapDataWriter NString(string? value) => value is null ? Text("NIL") : String(value);

    /// <summary>
    /// Writes a literal: <c>{n}</c>, CRLF, then the n bytes as they are.
    /// </summary>
    /// <param name="value">The bytes.</param>
    /// <returns>This writer.</returns>
    public ImapDataWriter Literal(ReadOnlySpan<byte> value)
    {
        Text("{").Number(value.Length).Text("}\r\n");
        bytes.Write(value);
        return this;
    }

    /// <summary>
    /// The bytes written so far.
    /// </summary>
    /// <returns>A copy of them.</returns>
    public byte[] ToArray() => bytes.ToArray();
}
