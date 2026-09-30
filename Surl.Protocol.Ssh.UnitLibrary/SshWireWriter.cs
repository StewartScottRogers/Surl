using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Writes the data types of RFC 4251 section 5 into a message payload.
/// </summary>
internal sealed class SshWireWriter
{
    private readonly ArrayBufferWriter<byte> buffer = new();

    /// <summary>
    /// Appends one <c>byte</c>.
    /// </summary>
    /// <param name="value">The byte.</param>
    public void WriteByte(byte value) => WriteBytes([value]);

    /// <summary>
    /// Appends a <c>boolean</c>: one byte, 1 for true and 0 for false.
    /// </summary>
    /// <param name="value">The value.</param>
    public void WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    /// <summary>
    /// Appends a <c>uint32</c>, most significant byte first.
    /// </summary>
    /// <param name="value">The value.</param>
    public void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(buffer.GetSpan(4), value);
        buffer.Advance(4);
    }

    /// <summary>
    /// Appends raw bytes with no length prefix.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    public void WriteBytes(ReadOnlySpan<byte> bytes) => buffer.Write(bytes);

    /// <summary>
    /// Appends a <c>string</c> holding <paramref name="text"/> as US-ASCII: its length as a
    /// <c>uint32</c>, then its bytes.
    /// </summary>
    /// <param name="text">The text.</param>
    public void WriteString(string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        WriteUInt32((uint)bytes.Length);
        WriteBytes(bytes);
    }

    /// <summary>
    /// Appends a <c>string</c> holding <paramref name="bytes"/>: their length as a
    /// <c>uint32</c>, then the bytes.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    public void WriteString(ReadOnlySpan<byte> bytes)
    {
        WriteUInt32((uint)bytes.Length);
        WriteBytes(bytes);
    }

    /// <summary>
    /// Appends an <c>mpint</c> (RFC 4251, section 5): two's complement, most significant byte
    /// first, in the fewest bytes, as a <c>string</c>; zero is the empty string.
    /// </summary>
    /// <param name="value">The value.</param>
    public void WriteMpint(BigInteger value) =>
        WriteString(value.IsZero ? [] : value.ToByteArray(isUnsigned: false, isBigEndian: true));

    /// <summary>
    /// Appends a <c>name-list</c>: the names joined by commas, as a US-ASCII <c>string</c>.
    /// </summary>
    /// <param name="names">The names, in preference order.</param>
    public void WriteNameList(IEnumerable<string> names) => WriteString(string.Join(',', names));

    /// <summary>
    /// Everything appended so far.
    /// </summary>
    /// <returns>A copy of the bytes.</returns>
    public byte[] ToArray() => buffer.WrittenSpan.ToArray();
}
