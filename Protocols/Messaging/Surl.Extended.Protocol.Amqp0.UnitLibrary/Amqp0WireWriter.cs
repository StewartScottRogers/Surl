using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Text;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// Writes AMQP 0-9-1's data types (section 4.2.5), all big-endian: octets, shorts, longs and
/// long-longs, packed bits, short and long strings, and field tables and arrays with every
/// errata type octet (<see cref="Amqp0FieldValueType"/>).
/// </summary>
public sealed class Amqp0WireWriter
{
    private const int ShortStringMaxBytes = byte.MaxValue;

    private static readonly FrozenDictionary<Amqp0FieldValueType, Action<Amqp0WireWriter, object?>> ValueWriters =
        new Dictionary<Amqp0FieldValueType, Action<Amqp0WireWriter, object?>>
        {
            [Amqp0FieldValueType.Boolean] = (writer, value) => writer.WriteOctet((bool)value! ? (byte)1 : (byte)0),
            [Amqp0FieldValueType.Int8] = (writer, value) => writer.WriteOctet((byte)(sbyte)value!),
            [Amqp0FieldValueType.UInt8] = (writer, value) => writer.WriteOctet((byte)value!),
            [Amqp0FieldValueType.Int16] = (writer, value) => writer.WriteShort((ushort)(short)value!),
            [Amqp0FieldValueType.UInt16] = (writer, value) => writer.WriteShort((ushort)value!),
            [Amqp0FieldValueType.Int32] = (writer, value) => writer.WriteLong((uint)(int)value!),
            [Amqp0FieldValueType.UInt32] = (writer, value) => writer.WriteLong((uint)value!),
            [Amqp0FieldValueType.Int64] = (writer, value) => writer.WriteLongLong((ulong)(long)value!),
            [Amqp0FieldValueType.Int64SpecificationOctet] = (writer, value) => writer.WriteLongLong((ulong)(long)value!),
            [Amqp0FieldValueType.Float32] = (writer, value) => writer.WriteLong(BitConverter.SingleToUInt32Bits((float)value!)),
            [Amqp0FieldValueType.Float64] = (writer, value) => writer.WriteLongLong(BitConverter.DoubleToUInt64Bits((double)value!)),
            [Amqp0FieldValueType.Decimal] = (writer, value) => writer.WriteDecimal((Amqp0Decimal)value!),
            [Amqp0FieldValueType.LongString] = (writer, value) => writer.WriteLongString((byte[])value!),
            [Amqp0FieldValueType.ByteArray] = (writer, value) => writer.WriteLongString((byte[])value!),
            [Amqp0FieldValueType.Array] = (writer, value) => writer.WriteFieldArray((IReadOnlyList<Amqp0FieldValue>)value!),
            [Amqp0FieldValueType.Timestamp] = (writer, value) => writer.WriteLongLong((ulong)value!),
            [Amqp0FieldValueType.Table] = (writer, value) => writer.WriteFieldTable((Amqp0FieldTable)value!),
            [Amqp0FieldValueType.Void] = (_, _) => { },
        }.ToFrozenDictionary();

    private readonly ArrayBufferWriter<byte> buffer = new();

    /// <summary>Writes an octet.</summary>
    /// <param name="value">The octet.</param>
    public void WriteOctet(byte value) => WriteBytes([value]);

    /// <summary>Writes a short, an unsigned 16-bit integer.</summary>
    /// <param name="value">The value.</param>
    public void WriteShort(ushort value) => BinaryPrimitives.WriteUInt16BigEndian(Reserve(2), value);

    /// <summary>Writes a long, an unsigned 32-bit integer.</summary>
    /// <param name="value">The value.</param>
    public void WriteLong(uint value) => BinaryPrimitives.WriteUInt32BigEndian(Reserve(4), value);

    /// <summary>Writes a long-long, an unsigned 64-bit integer (also a timestamp).</summary>
    /// <param name="value">The value.</param>
    public void WriteLongLong(ulong value) => BinaryPrimitives.WriteUInt64BigEndian(Reserve(8), value);

    /// <summary>
    /// Writes consecutive bit fields, packed eight to an octet with the first in the octet's
    /// lowest bit (section 4.2.5.2).
    /// </summary>
    /// <param name="bits">The bits, in order; at least one.</param>
    /// <exception cref="ArgumentException"><paramref name="bits"/> is empty.</exception>
    public void WriteBits(ReadOnlySpan<bool> bits)
    {
        if (bits.IsEmpty)
        {
            throw new ArgumentException("At least one bit is written.", nameof(bits));
        }

        var octets = Reserve((bits.Length + 7) / 8);
        octets.Clear();
        for (var index = 0; index < bits.Length; index++)
        {
            if (bits[index])
            {
                octets[index / 8] |= (byte)(1 << (index % 8));
            }
        }
    }

    /// <summary>Writes a short string: a length octet and the text's UTF-8.</summary>
    /// <param name="text">The text; at most 255 bytes of UTF-8.</param>
    /// <exception cref="ArgumentException"><paramref name="text"/> is longer than 255 bytes of UTF-8.</exception>
    public void WriteShortString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var utf8 = Encoding.UTF8.GetBytes(text);
        if (utf8.Length > ShortStringMaxBytes)
        {
            throw new ArgumentException("A short string holds at most 255 bytes.", nameof(text));
        }

        WriteOctet((byte)utf8.Length);
        WriteBytes(utf8);
    }

    /// <summary>Writes a long string: a long length and the octets.</summary>
    /// <param name="value">The octets.</param>
    public void WriteLongString(ReadOnlySpan<byte> value)
    {
        WriteLong((uint)value.Length);
        WriteBytes(value);
    }

    /// <summary>Writes a field table: a long size, then each name and value.</summary>
    /// <param name="table">The table.</param>
    public void WriteFieldTable(Amqp0FieldTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var nested = new Amqp0WireWriter();
        foreach (var field in table.Fields)
        {
            nested.WriteShortString(field.Name);
            nested.WriteFieldValue(field.Value);
        }

        WriteLongString(nested.buffer.WrittenSpan);
    }

    /// <summary>Writes a field array: a long size, then each value.</summary>
    /// <param name="values">The values, in order.</param>
    public void WriteFieldArray(IReadOnlyList<Amqp0FieldValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var nested = new Amqp0WireWriter();
        foreach (var value in values)
        {
            nested.WriteFieldValue(value);
        }

        WriteLongString(nested.buffer.WrittenSpan);
    }

    /// <summary>Writes one field value: its type octet, then the value.</summary>
    /// <param name="value">The value.</param>
    public void WriteFieldValue(Amqp0FieldValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        WriteOctet((byte)value.Type);
        ValueWriters[value.Type](this, value.Value);
    }

    /// <summary>Writes octets as they are.</summary>
    /// <param name="value">The octets.</param>
    public void WriteBytes(ReadOnlySpan<byte> value) => buffer.Write(value);

    /// <summary>The bytes written so far.</summary>
    /// <returns>A copy of them.</returns>
    public byte[] ToArray() => buffer.WrittenSpan.ToArray();

    private void WriteDecimal(Amqp0Decimal value)
    {
        WriteOctet(value.Scale);
        WriteLong(value.Value);
    }

    private Span<byte> Reserve(int count)
    {
        var span = buffer.GetSpan(count)[..count];
        buffer.Advance(count);
        return span;
    }
}
