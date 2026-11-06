using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Writes the Kafka protocol guide's primitive types ("Protocol Primitive Types") into a growing
/// buffer, front to back, as <see cref="KafkaPrimitiveReader"/> reads them.
/// </summary>
public sealed class KafkaPrimitiveWriter
{
    private readonly ArrayBufferWriter<byte> buffer = new();

    /// <summary>How many bytes have been written.</summary>
    public int Length => buffer.WrittenCount;

    /// <summary>Returns a copy of every byte written so far.</summary>
    /// <returns>The bytes.</returns>
    public byte[] ToArray() => buffer.WrittenSpan.ToArray();

    /// <summary>Writes a BOOLEAN: 1 for true, 0 for false.</summary>
    /// <param name="value">The value.</param>
    public void WriteBoolean(bool value) => WriteRaw([value ? (byte)1 : (byte)0]);

    /// <summary>Writes an INT8.</summary>
    /// <param name="value">The value.</param>
    public void WriteInt8(sbyte value) => WriteRaw([(byte)value]);

    /// <summary>Writes an INT16.</summary>
    /// <param name="value">The value.</param>
    public void WriteInt16(short value) => BinaryPrimitives.WriteInt16BigEndian(Reserve(2), value);

    /// <summary>Writes an INT32.</summary>
    /// <param name="value">The value.</param>
    public void WriteInt32(int value) => BinaryPrimitives.WriteInt32BigEndian(Reserve(4), value);

    /// <summary>Writes an INT64.</summary>
    /// <param name="value">The value.</param>
    public void WriteInt64(long value) => BinaryPrimitives.WriteInt64BigEndian(Reserve(8), value);

    /// <summary>Writes a UINT32.</summary>
    /// <param name="value">The value.</param>
    public void WriteUInt32(uint value) => BinaryPrimitives.WriteUInt32BigEndian(Reserve(4), value);

    /// <summary>Writes a FLOAT64, an IEEE 754 binary64 value.</summary>
    /// <param name="value">The value.</param>
    public void WriteFloat64(double value) => BinaryPrimitives.WriteDoubleBigEndian(Reserve(8), value);

    /// <summary>Writes a UUID: sixteen bytes, the most significant first.</summary>
    /// <param name="value">The value.</param>
    public void WriteUuid(Guid value) => value.TryWriteBytes(Reserve(16), bigEndian: true, out _);

    /// <summary>Writes an UNSIGNED_VARINT: seven bits a byte, low first, the high bit set on all but the last.</summary>
    /// <param name="value">The value.</param>
    public void WriteUnsignedVarInt(uint value) => WriteVariableLength(value);

    /// <summary>Writes a VARINT: the value zigzag-encoded as an UNSIGNED_VARINT.</summary>
    /// <param name="value">The value.</param>
    public void WriteVarInt(int value) => WriteVariableLength((uint)((value << 1) ^ (value >> 31)));

    /// <summary>Writes a VARLONG: the value zigzag-encoded, seven bits a byte.</summary>
    /// <param name="value">The value.</param>
    public void WriteVarLong(long value) => WriteVariableLength((ulong)((value << 1) ^ (value >> 63)));

    /// <summary>Writes a STRING: an INT16 length, then UTF-8.</summary>
    /// <param name="value">The value, at most 32767 bytes of UTF-8.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is over 32767 bytes of UTF-8.</exception>
    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteNullableString(value);
    }

    /// <summary>Writes a NULLABLE_STRING: an INT16 length, -1 for null, then UTF-8.</summary>
    /// <param name="value">The value, at most 32767 bytes of UTF-8, or null.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is over 32767 bytes of UTF-8.</exception>
    public void WriteNullableString(string? value)
    {
        if (value is null)
        {
            WriteInt16(-1);
            return;
        }

        var utf8 = Encoding.UTF8.GetBytes(value);
        if (utf8.Length > short.MaxValue)
        {
            throw new ArgumentException($"A STRING holds at most {short.MaxValue} bytes; this one is {utf8.Length}.", nameof(value));
        }

        WriteInt16((short)utf8.Length);
        WriteRaw(utf8);
    }

    /// <summary>Writes a COMPACT_STRING: an UNSIGNED_VARINT length plus one, then UTF-8.</summary>
    /// <param name="value">The value.</param>
    public void WriteCompactString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteCompactNullableString(value);
    }

    /// <summary>Writes a COMPACT_NULLABLE_STRING: an UNSIGNED_VARINT length plus one, 0 for null, then UTF-8.</summary>
    /// <param name="value">The value, or null.</param>
    public void WriteCompactNullableString(string? value) =>
        WriteCompactNullableBytes(value is null ? (ReadOnlyMemory<byte>?)null : Encoding.UTF8.GetBytes(value));

    /// <summary>Writes BYTES: an INT32 length, then the bytes.</summary>
    /// <param name="value">The value.</param>
    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        WriteInt32(value.Length);
        WriteRaw(value);
    }

    /// <summary>Writes NULLABLE_BYTES, and RECORDS in a non-flexible version: an INT32 length, -1 for null, then the bytes.</summary>
    /// <param name="value">The value, or null.</param>
    public void WriteNullableBytes(ReadOnlyMemory<byte>? value)
    {
        if (value is not { } bytes)
        {
            WriteInt32(-1);
            return;
        }

        WriteBytes(bytes.Span);
    }

    /// <summary>Writes COMPACT_BYTES: an UNSIGNED_VARINT length plus one, then the bytes.</summary>
    /// <param name="value">The value.</param>
    public void WriteCompactBytes(ReadOnlySpan<byte> value)
    {
        WriteUnsignedVarInt((uint)value.Length + 1);
        WriteRaw(value);
    }

    /// <summary>
    /// Writes COMPACT_NULLABLE_BYTES, and RECORDS in a flexible version: an UNSIGNED_VARINT
    /// length plus one, 0 for null, then the bytes.
    /// </summary>
    /// <param name="value">The value, or null.</param>
    public void WriteCompactNullableBytes(ReadOnlyMemory<byte>? value)
    {
        if (value is not { } bytes)
        {
            WriteUnsignedVarInt(0);
            return;
        }

        WriteCompactBytes(bytes.Span);
    }

    /// <summary>Writes an ARRAY's element count: an INT32, -1 for a null array.</summary>
    /// <param name="count">The count, or null.</param>
    public void WriteArrayLength(int? count) => WriteInt32(count ?? -1);

    /// <summary>Writes a COMPACT_ARRAY's element count: an UNSIGNED_VARINT count plus one, 0 for null.</summary>
    /// <param name="count">The count, or null.</param>
    public void WriteCompactArrayLength(int? count) => WriteUnsignedVarInt(count is { } value ? (uint)value + 1 : 0);

    /// <summary>Writes a tagged-field section holding no field: a count of 0.</summary>
    public void WriteEmptyTaggedFields() => WriteUnsignedVarInt(0);

    /// <summary>Writes <paramref name="bytes"/> as they are.</summary>
    /// <param name="bytes">The bytes.</param>
    public void WriteRaw(ReadOnlySpan<byte> bytes) => buffer.Write(bytes);

    private void WriteVariableLength(ulong value)
    {
        while (value >= 0x80)
        {
            WriteRaw([(byte)(value | 0x80)]);
            value >>= 7;
        }

        WriteRaw([(byte)value]);
    }

    private Span<byte> Reserve(int count)
    {
        var span = buffer.GetSpan(count)[..count];
        buffer.Advance(count);
        return span;
    }
}
