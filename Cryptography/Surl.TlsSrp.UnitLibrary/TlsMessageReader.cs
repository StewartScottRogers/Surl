using System.Buffers.Binary;

namespace Surl.TlsSrp;

/// <summary>
/// Reads the fields of one handshake message body or extension (RFC 5246 section 4): fixed-width
/// big-endian numbers and length-prefixed vectors. Running out of bytes, or bytes left over when
/// the caller expects the end, is <see cref="TlsAlertDescription.DecodeError"/>.
/// </summary>
/// <param name="bytes">The bytes to read.</param>
/// <param name="what">What the bytes are, for the alert's message.</param>
internal sealed class TlsMessageReader(byte[] bytes, string what)
{
    private int position;

    /// <summary>Whether every byte has been read.</summary>
    public bool IsAtEnd => position == bytes.Length;

    /// <summary>Reads one byte.</summary>
    public byte ReadByte() => Take(1)[0];

    /// <summary>Reads a two-byte big-endian number.</summary>
    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(Take(2));

    /// <summary>Reads exactly <paramref name="count"/> bytes.</summary>
    /// <param name="count">How many bytes.</param>
    public byte[] ReadBytes(int count) => Take(count).ToArray();

    /// <summary>Reads a vector with a one-byte length (<c>&lt;0..2^8-1&gt;</c>).</summary>
    public byte[] ReadVector8() => ReadBytes(ReadByte());

    /// <summary>Reads a vector with a two-byte length (<c>&lt;0..2^16-1&gt;</c>).</summary>
    public byte[] ReadVector16() => ReadBytes(ReadUInt16());

    /// <summary>Refuses bytes left over after the last field.</summary>
    /// <exception cref="TlsAlertException"><see cref="TlsAlertDescription.DecodeError"/> when bytes are left.</exception>
    public void ExpectEnd()
    {
        if (!IsAtEnd)
        {
            throw DecodeError($"{bytes.Length - position} bytes are left over at the end of {what}.");
        }
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (bytes.Length - position < count)
        {
            throw DecodeError($"{what} ends before a field of {count} bytes.");
        }

        var taken = bytes.AsSpan(position, count);
        position += count;
        return taken;
    }

    private static TlsAlertException DecodeError(string message) => new(TlsAlertDescription.DecodeError, message);
}
