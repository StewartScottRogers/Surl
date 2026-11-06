using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Text;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// Reads AMQP 0-9-1's data types (section 4.2.5), all big-endian, from one payload: octets,
/// shorts, longs and long-longs, packed bits, short and long strings, and field tables and arrays
/// with every errata type octet (<see cref="Amqp0FieldValueType"/>). A read that cannot complete
/// throws <see cref="Amqp0SyntaxException"/>.
/// </summary>
public sealed class Amqp0WireReader
{
    /// <summary>
    /// How deep tables and arrays may nest inside the outermost table: 64. Deeper nesting is a
    /// syntax error, so a hostile frame cannot run the reader out of stack.
    /// </summary>
    public const int MaxNestingDepth = 64;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly FrozenDictionary<Amqp0FieldValueType, Func<Amqp0WireReader, Amqp0FieldValue>> ValueReaders =
        new Dictionary<Amqp0FieldValueType, Func<Amqp0WireReader, Amqp0FieldValue>>
        {
            [Amqp0FieldValueType.Boolean] = reader => Amqp0FieldValue.Boolean(reader.ReadOctet() != 0),
            [Amqp0FieldValueType.Int8] = reader => Amqp0FieldValue.Int8((sbyte)reader.ReadOctet()),
            [Amqp0FieldValueType.UInt8] = reader => Amqp0FieldValue.UInt8(reader.ReadOctet()),
            [Amqp0FieldValueType.Int16] = reader => Amqp0FieldValue.Int16((short)reader.ReadShort()),
            [Amqp0FieldValueType.UInt16] = reader => Amqp0FieldValue.UInt16(reader.ReadShort()),
            [Amqp0FieldValueType.Int32] = reader => Amqp0FieldValue.Int32((int)reader.ReadLong()),
            [Amqp0FieldValueType.UInt32] = reader => Amqp0FieldValue.UInt32(reader.ReadLong()),
            [Amqp0FieldValueType.Int64] = reader => Amqp0FieldValue.Int64((long)reader.ReadLongLong()),
            [Amqp0FieldValueType.Int64SpecificationOctet] = reader => Amqp0FieldValue.Int64SpecificationOctet((long)reader.ReadLongLong()),
            [Amqp0FieldValueType.Float32] = reader => Amqp0FieldValue.Float32(BitConverter.UInt32BitsToSingle(reader.ReadLong())),
            [Amqp0FieldValueType.Float64] = reader => Amqp0FieldValue.Float64(BitConverter.UInt64BitsToDouble(reader.ReadLongLong())),
            [Amqp0FieldValueType.Decimal] = reader => Amqp0FieldValue.Decimal(new(reader.ReadOctet(), reader.ReadLong())),
            [Amqp0FieldValueType.LongString] = reader => Amqp0FieldValue.LongString(reader.ReadLongString()),
            [Amqp0FieldValueType.ByteArray] = reader => Amqp0FieldValue.ByteArray(reader.ReadLongString()),
            [Amqp0FieldValueType.Array] = reader => Amqp0FieldValue.Array(reader.ReadFieldArray()),
            [Amqp0FieldValueType.Timestamp] = reader => Amqp0FieldValue.Timestamp(reader.ReadLongLong()),
            [Amqp0FieldValueType.Table] = reader => Amqp0FieldValue.Table(reader.ReadFieldTable()),
            [Amqp0FieldValueType.Void] = _ => Amqp0FieldValue.Void,
        }.ToFrozenDictionary();

    private readonly ReadOnlyMemory<byte> bytes;
    private readonly int depth;
    private int position;

    /// <summary>Creates a reader over <paramref name="bytes"/>, from its first byte.</summary>
    /// <param name="bytes">The payload to read.</param>
    public Amqp0WireReader(ReadOnlyMemory<byte> bytes)
        : this(bytes, 0)
    {
    }

    private Amqp0WireReader(ReadOnlyMemory<byte> bytes, int depth)
    {
        this.bytes = bytes;
        this.depth = depth;
    }

    /// <summary>How many bytes are left to read.</summary>
    public int Remaining => bytes.Length - position;

    /// <summary>Reads an octet.</summary>
    /// <returns>The octet.</returns>
    public byte ReadOctet() => Take(1)[0];

    /// <summary>Reads a short, an unsigned 16-bit integer.</summary>
    /// <returns>The value.</returns>
    public ushort ReadShort() => BinaryPrimitives.ReadUInt16BigEndian(Take(2));

    /// <summary>Reads a long, an unsigned 32-bit integer.</summary>
    /// <returns>The value.</returns>
    public uint ReadLong() => BinaryPrimitives.ReadUInt32BigEndian(Take(4));

    /// <summary>Reads a long-long, an unsigned 64-bit integer (also a timestamp).</summary>
    /// <returns>The value.</returns>
    public ulong ReadLongLong() => BinaryPrimitives.ReadUInt64BigEndian(Take(8));

    /// <summary>
    /// Reads <paramref name="count"/> consecutive bit fields, packed eight to an octet with the
    /// first in the octet's lowest bit (section 4.2.5.2).
    /// </summary>
    /// <param name="count">How many bits; 1 or more.</param>
    /// <returns>The bits, in order.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is below 1.</exception>
    public bool[] ReadBits(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var octets = Take((count + 7) / 8);
        var bits = new bool[count];
        for (var index = 0; index < count; index++)
        {
            bits[index] = (octets[index / 8] & (1 << (index % 8))) != 0;
        }

        return bits;
    }

    /// <summary>Reads a short string: a length octet and that many octets of UTF-8.</summary>
    /// <returns>The text.</returns>
    public string ReadShortString()
    {
        var text = Take(ReadOctet());
        try
        {
            return StrictUtf8.GetString(text);
        }
        catch (DecoderFallbackException exception)
        {
            throw new Amqp0SyntaxException("A short string is not well-formed UTF-8.", exception);
        }
    }

    /// <summary>Reads a long string: a long length and that many octets.</summary>
    /// <returns>The octets.</returns>
    public byte[] ReadLongString() => Take(ReadLength()).ToArray();

    /// <summary>Reads a field table: a long size, then name-value pairs filling exactly that many bytes.</summary>
    /// <returns>The table.</returns>
    public Amqp0FieldTable ReadFieldTable()
    {
        var table = Nested();
        var fields = new List<Amqp0Field>();
        while (table.Remaining > 0)
        {
            var name = table.ReadShortString();
            fields.Add(new(name, table.ReadFieldValue()));
        }

        return new(fields);
    }

    /// <summary>Reads a field array: a long size, then values filling exactly that many bytes.</summary>
    /// <returns>The values, in order.</returns>
    public IReadOnlyList<Amqp0FieldValue> ReadFieldArray()
    {
        var array = Nested();
        var values = new List<Amqp0FieldValue>();
        while (array.Remaining > 0)
        {
            values.Add(array.ReadFieldValue());
        }

        return values;
    }

    /// <summary>Reads one field value: its type octet, then the value that type says.</summary>
    /// <returns>The value.</returns>
    public Amqp0FieldValue ReadFieldValue()
    {
        var type = (Amqp0FieldValueType)ReadOctet();
        if (!ValueReaders.TryGetValue(type, out var readValue))
        {
            throw new Amqp0SyntaxException("A field value has an unknown type octet.");
        }

        return readValue(this);
    }

    /// <summary>Reads every byte left.</summary>
    /// <returns>The bytes; empty when none are left.</returns>
    public ReadOnlyMemory<byte> ReadRemaining()
    {
        var rest = bytes[position..];
        position = bytes.Length;
        return rest;
    }

    private Amqp0WireReader Nested()
    {
        var size = ReadLength();
        if (depth > MaxNestingDepth)
        {
            throw new Amqp0SyntaxException("Field tables and arrays nest too deep.");
        }

        var nested = new Amqp0WireReader(bytes.Slice(position, size), depth + 1);
        position += size;
        return nested;
    }

    private int ReadLength()
    {
        var length = ReadLong();
        if (length > (uint)Remaining)
        {
            throw new Amqp0SyntaxException("A length runs past the payload.");
        }

        return (int)length;
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count > Remaining)
        {
            throw new Amqp0SyntaxException("A field runs past the payload.");
        }

        var span = bytes.Span.Slice(position, count);
        position += count;
        return span;
    }
}
