using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Reads the data types of RFC 4251 section 5 from a message payload, front to back. A
/// field that runs past the payload's end is a malformed message, refused with
/// <c>DISCONNECT</c> 2 (ADR-0051, decision 9).
/// </summary>
/// <param name="payload">The payload to read.</param>
internal sealed class SshWireReader(ReadOnlyMemory<byte> payload)
{
    private int position;

    /// <summary>
    /// Reads one <c>byte</c>.
    /// </summary>
    /// <returns>The byte.</returns>
    /// <exception cref="SshDisconnectRequiredException">The payload has ended.</exception>
    public byte ReadByte() => ReadBytes(1).Span[0];

    /// <summary>
    /// Reads a <c>boolean</c>: any non-zero byte is true.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="SshDisconnectRequiredException">The payload has ended.</exception>
    public bool ReadBoolean() => ReadByte() != 0;

    /// <summary>
    /// Reads a <c>uint32</c>, most significant byte first.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="SshDisconnectRequiredException">Fewer than four bytes remain.</exception>
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(ReadBytes(4).Span);

    /// <summary>
    /// Reads <paramref name="count"/> raw bytes.
    /// </summary>
    /// <param name="count">How many bytes to read.</param>
    /// <returns>The bytes, a slice of the payload.</returns>
    /// <exception cref="SshDisconnectRequiredException">Fewer than <paramref name="count"/> bytes remain.</exception>
    public ReadOnlyMemory<byte> ReadBytes(long count)
    {
        if (count > payload.Length - position)
        {
            throw SshDisconnectRequiredException.ProtocolError("An SSH message ended inside a field.");
        }

        var bytes = payload.Slice(position, (int)count);
        position += (int)count;

        return bytes;
    }

    /// <summary>
    /// Reads a <c>string</c>: a <c>uint32</c> length, then that many bytes.
    /// </summary>
    /// <returns>The string's bytes, a slice of the payload.</returns>
    /// <exception cref="SshDisconnectRequiredException">The length runs past the payload's end.</exception>
    public ReadOnlyMemory<byte> ReadString() => ReadBytes(ReadUInt32());

    /// <summary>
    /// Reads an <c>mpint</c> (RFC 4251, section 5): a <c>string</c> holding a two's complement
    /// integer, most significant byte first; the empty string is zero.
    /// </summary>
    /// <returns>The value, negative when its first bit is set.</returns>
    /// <exception cref="SshDisconnectRequiredException">The length runs past the payload's end.</exception>
    public BigInteger ReadMpint() => new(ReadString().Span, isUnsigned: false, isBigEndian: true);

    /// <summary>
    /// Reads a <c>name-list</c>: a <c>string</c> of comma-separated names. An empty string is
    /// an empty list. Each byte becomes one character (Latin-1), so a name holding a byte
    /// outside US-ASCII matches no algorithm and still reaches the log as it was sent.
    /// </summary>
    /// <returns>The names, in the order given.</returns>
    /// <exception cref="SshDisconnectRequiredException">The length runs past the payload's end.</exception>
    public IReadOnlyList<string> ReadNameList()
    {
        var bytes = ReadString();

        return bytes.IsEmpty ? [] : Encoding.Latin1.GetString(bytes.Span).Split(',');
    }
}
