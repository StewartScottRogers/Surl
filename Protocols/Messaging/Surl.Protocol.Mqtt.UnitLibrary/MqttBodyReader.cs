using System.Buffers.Binary;
using System.Text;
using System.Text.Unicode;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// Reads the fields of an MQTT packet's body in order: two-byte integers, UTF-8 strings and
/// single bytes (MQTT 3.1.1, section 1.5). Every read says whether the field was there; a
/// field that runs past the end of the body is not read and leaves the position unchanged.
/// </summary>
internal sealed class MqttBodyReader
{
    private readonly byte[] body;
    private int position;

    /// <summary>
    /// Creates a reader positioned at the start of <paramref name="body"/>.
    /// </summary>
    /// <param name="body">The packet's variable header and payload.</param>
    public MqttBodyReader(byte[] body)
    {
        this.body = body;
    }

    /// <summary>
    /// Whether every byte of the body has been read.
    /// </summary>
    public bool IsAtEnd => position == body.Length;

    /// <summary>
    /// The bytes not yet read.
    /// </summary>
    public ReadOnlySpan<byte> Remaining => body.AsSpan(position);

    /// <summary>
    /// Reads one byte.
    /// </summary>
    /// <param name="value">The byte, or 0 when there is none.</param>
    /// <returns>Whether a byte was there.</returns>
    public bool TryReadByte(out byte value)
    {
        value = 0;
        if (IsAtEnd)
        {
            return false;
        }

        value = body[position++];
        return true;
    }

    /// <summary>
    /// Reads a big-endian two-byte integer (MQTT 3.1.1, section 1.5.2).
    /// </summary>
    /// <param name="value">The integer, or 0 when there is none.</param>
    /// <returns>Whether two bytes were there.</returns>
    public bool TryReadUInt16(out ushort value)
    {
        value = 0;
        if (Remaining.Length < sizeof(ushort))
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16BigEndian(Remaining);
        position += sizeof(ushort);
        return true;
    }

    /// <summary>
    /// Reads a UTF-8 string with its two-byte length prefix (MQTT 3.1.1, section 1.5.3).
    /// </summary>
    /// <param name="value">The string, or empty when there is none.</param>
    /// <returns>
    /// Whether the whole string was there as well-formed UTF-8 without U+0000, which section
    /// 1.5.3 forbids.
    /// </returns>
    public bool TryReadString(out string value)
    {
        value = string.Empty;
        if (Remaining.Length < sizeof(ushort))
        {
            return false;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(Remaining);
        var bytes = Remaining[sizeof(ushort)..];
        if (bytes.Length < length || !IsWellFormedString(bytes[..length]))
        {
            return false;
        }

        value = Encoding.UTF8.GetString(bytes[..length]);
        position += sizeof(ushort) + length;
        return true;
    }

    /// <summary>
    /// Reads binary data with its two-byte length prefix, as a will message and a password
    /// are sent (MQTT 3.1.1, sections 3.1.3.3 and 3.1.3.5).
    /// </summary>
    /// <param name="value">The bytes, or empty when there are none.</param>
    /// <returns>Whether the length and every byte it announces were there.</returns>
    public bool TryReadBinary(out ReadOnlyMemory<byte> value)
    {
        value = ReadOnlyMemory<byte>.Empty;
        if (Remaining.Length < sizeof(ushort))
        {
            return false;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(Remaining);
        if (Remaining.Length - sizeof(ushort) < length)
        {
            return false;
        }

        value = body.AsMemory(position + sizeof(ushort), length);
        position += sizeof(ushort) + length;
        return true;
    }

    private static bool IsWellFormedString(ReadOnlySpan<byte> bytes) => Utf8.IsValid(bytes) && !bytes.Contains((byte)0);
}
