using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Writes AMQP 1.0 values (Part 1 sections 1.2 to 1.6), each in the smallest encoding that holds it.
/// </summary>
/// <remarks>
/// <para>
/// A value is given as the .NET type <see cref="Amqp1Decoder"/> reads it as. Its format code is
/// <see cref="FormatCodeFor"/>'s: the zero-width form where there is one (<c>true</c>,
/// <c>false</c>, <c>uint0</c>, <c>ulong0</c>, <c>list0</c>), then the one-octet form where the value
/// fits (<c>smalluint</c>, <c>smallulong</c>, <c>smallint</c>, <c>smalllong</c>, and the 8-bit
/// widths of <c>binary</c>, <c>string</c>, <c>symbol</c>, <c>list</c>, <c>map</c> and
/// <c>array</c>), else the wide form.
/// </para>
/// <para>
/// An <see cref="Amqp1Array"/> is written with its own <see cref="Amqp1Array.ElementFormatCode"/>,
/// so an array read is written back the same; it is the only way the one-octet
/// <c>boolean</c> (0x56) is written. An element that does not fit that format code throws.
/// </para>
/// </remarks>
public sealed class Amqp1Encoder
{
    private static readonly Dictionary<Type, Func<object, byte>> FormatCodeChoosers = new()
    {
        [typeof(bool)] = value => (bool)value ? Amqp1FormatCode.BooleanTrue : Amqp1FormatCode.BooleanFalse,
        [typeof(byte)] = _ => Amqp1FormatCode.UByte,
        [typeof(sbyte)] = _ => Amqp1FormatCode.Byte,
        [typeof(ushort)] = _ => Amqp1FormatCode.UShort,
        [typeof(short)] = _ => Amqp1FormatCode.Short,
        [typeof(uint)] = value => UnsignedFormatCode((uint)value, Amqp1FormatCode.UInt0, Amqp1FormatCode.SmallUInt, Amqp1FormatCode.UInt),
        [typeof(ulong)] = value => UnsignedFormatCode((ulong)value, Amqp1FormatCode.ULong0, Amqp1FormatCode.SmallULong, Amqp1FormatCode.ULong),
        [typeof(int)] = value => SignedFormatCode((int)value, Amqp1FormatCode.SmallInt, Amqp1FormatCode.Int),
        [typeof(long)] = value => SignedFormatCode((long)value, Amqp1FormatCode.SmallLong, Amqp1FormatCode.Long),
        [typeof(float)] = _ => Amqp1FormatCode.Float,
        [typeof(double)] = _ => Amqp1FormatCode.Double,
        [typeof(Amqp1Decimal32)] = _ => Amqp1FormatCode.Decimal32,
        [typeof(Amqp1Decimal64)] = _ => Amqp1FormatCode.Decimal64,
        [typeof(Amqp1Decimal128)] = _ => Amqp1FormatCode.Decimal128,
        [typeof(Rune)] = _ => Amqp1FormatCode.Char,
        [typeof(Amqp1Timestamp)] = _ => Amqp1FormatCode.Timestamp,
        [typeof(Guid)] = _ => Amqp1FormatCode.Uuid,
        [typeof(byte[])] = value => VariableWidthFormatCode(((byte[])value).Length, Amqp1FormatCode.Binary8, Amqp1FormatCode.Binary32),
        [typeof(string)] = value => VariableWidthFormatCode(Encoding.UTF8.GetByteCount((string)value), Amqp1FormatCode.String8, Amqp1FormatCode.String32),
        [typeof(Amqp1Symbol)] = value => VariableWidthFormatCode(((Amqp1Symbol)value).Value.Length, Amqp1FormatCode.Symbol8, Amqp1FormatCode.Symbol32),
        [typeof(Amqp1Map)] = value => CompositeFormatCode(MapCount((Amqp1Map)value), MapContent((Amqp1Map)value).Length, Amqp1FormatCode.Map8, Amqp1FormatCode.Map32),
        [typeof(Amqp1Array)] = value => CompositeFormatCode(((Amqp1Array)value).Elements.Count, ArrayContent((Amqp1Array)value).Length, Amqp1FormatCode.Array8, Amqp1FormatCode.Array32),
    };

    private static readonly Dictionary<byte, Action<Amqp1Encoder, object?>> BodyWriters = new()
    {
        [Amqp1FormatCode.Null] = (_, _) => { },
        [Amqp1FormatCode.BooleanTrue] = (_, _) => { },
        [Amqp1FormatCode.BooleanFalse] = (_, _) => { },
        [Amqp1FormatCode.UInt0] = (_, _) => { },
        [Amqp1FormatCode.ULong0] = (_, _) => { },
        [Amqp1FormatCode.List0] = (_, _) => { },
        [Amqp1FormatCode.UByte] = (encoder, value) => encoder.WriteOctet((byte)value!),
        [Amqp1FormatCode.Byte] = (encoder, value) => encoder.WriteOctet((byte)(sbyte)value!),
        [Amqp1FormatCode.SmallUInt] = (encoder, value) => encoder.WriteOctet(checked((byte)(uint)value!)),
        [Amqp1FormatCode.SmallULong] = (encoder, value) => encoder.WriteOctet(checked((byte)(ulong)value!)),
        [Amqp1FormatCode.SmallInt] = (encoder, value) => encoder.WriteOctet((byte)checked((sbyte)(int)value!)),
        [Amqp1FormatCode.SmallLong] = (encoder, value) => encoder.WriteOctet((byte)checked((sbyte)(long)value!)),
        [Amqp1FormatCode.Boolean] = (encoder, value) => encoder.WriteOctet(Convert.ToByte((bool)value!)),
        [Amqp1FormatCode.UShort] = (encoder, value) => BinaryPrimitives.WriteUInt16BigEndian(encoder.Grow(2), (ushort)value!),
        [Amqp1FormatCode.Short] = (encoder, value) => BinaryPrimitives.WriteInt16BigEndian(encoder.Grow(2), (short)value!),
        [Amqp1FormatCode.UInt] = (encoder, value) => BinaryPrimitives.WriteUInt32BigEndian(encoder.Grow(4), (uint)value!),
        [Amqp1FormatCode.Int] = (encoder, value) => BinaryPrimitives.WriteInt32BigEndian(encoder.Grow(4), (int)value!),
        [Amqp1FormatCode.Float] = (encoder, value) => BinaryPrimitives.WriteSingleBigEndian(encoder.Grow(4), (float)value!),
        [Amqp1FormatCode.Char] = (encoder, value) => BinaryPrimitives.WriteUInt32BigEndian(encoder.Grow(4), (uint)((Rune)value!).Value),
        [Amqp1FormatCode.Decimal32] = (encoder, value) => BinaryPrimitives.WriteUInt32BigEndian(encoder.Grow(4), ((Amqp1Decimal32)value!).Bits),
        [Amqp1FormatCode.ULong] = (encoder, value) => BinaryPrimitives.WriteUInt64BigEndian(encoder.Grow(8), (ulong)value!),
        [Amqp1FormatCode.Long] = (encoder, value) => BinaryPrimitives.WriteInt64BigEndian(encoder.Grow(8), (long)value!),
        [Amqp1FormatCode.Double] = (encoder, value) => BinaryPrimitives.WriteDoubleBigEndian(encoder.Grow(8), (double)value!),
        [Amqp1FormatCode.Timestamp] = (encoder, value) => BinaryPrimitives.WriteInt64BigEndian(encoder.Grow(8), ((Amqp1Timestamp)value!).MillisecondsSinceUnixEpoch),
        [Amqp1FormatCode.Decimal64] = (encoder, value) => BinaryPrimitives.WriteUInt64BigEndian(encoder.Grow(8), ((Amqp1Decimal64)value!).Bits),
        [Amqp1FormatCode.Decimal128] = (encoder, value) => BinaryPrimitives.WriteUInt128BigEndian(encoder.Grow(16), ((Amqp1Decimal128)value!).Bits),
        [Amqp1FormatCode.Uuid] = (encoder, value) => encoder.WriteOctets(((Guid)value!).ToByteArray(bigEndian: true)),
        [Amqp1FormatCode.Binary8] = (encoder, value) => encoder.WriteSized(1, (byte[])value!),
        [Amqp1FormatCode.Binary32] = (encoder, value) => encoder.WriteSized(4, (byte[])value!),
        [Amqp1FormatCode.String8] = (encoder, value) => encoder.WriteSized(1, Encoding.UTF8.GetBytes((string)value!)),
        [Amqp1FormatCode.String32] = (encoder, value) => encoder.WriteSized(4, Encoding.UTF8.GetBytes((string)value!)),
        [Amqp1FormatCode.Symbol8] = (encoder, value) => encoder.WriteSized(1, SymbolBytes((Amqp1Symbol)value!)),
        [Amqp1FormatCode.Symbol32] = (encoder, value) => encoder.WriteSized(4, SymbolBytes((Amqp1Symbol)value!)),
        [Amqp1FormatCode.List8] = (encoder, value) => encoder.WriteComposite(1, ((IReadOnlyList<object?>)value!).Count, EncodeEach((IReadOnlyList<object?>)value!)),
        [Amqp1FormatCode.List32] = (encoder, value) => encoder.WriteComposite(4, ((IReadOnlyList<object?>)value!).Count, EncodeEach((IReadOnlyList<object?>)value!)),
        [Amqp1FormatCode.Map8] = (encoder, value) => encoder.WriteComposite(1, MapCount((Amqp1Map)value!), MapContent((Amqp1Map)value!)),
        [Amqp1FormatCode.Map32] = (encoder, value) => encoder.WriteComposite(4, MapCount((Amqp1Map)value!), MapContent((Amqp1Map)value!)),
        [Amqp1FormatCode.Array8] = (encoder, value) => encoder.WriteComposite(1, ((Amqp1Array)value!).Elements.Count, ArrayContent((Amqp1Array)value!)),
        [Amqp1FormatCode.Array32] = (encoder, value) => encoder.WriteComposite(4, ((Amqp1Array)value!).Elements.Count, ArrayContent((Amqp1Array)value!)),
    };

    private readonly ArrayBufferWriter<byte> output = new();

    /// <summary>The bytes written so far.</summary>
    public ReadOnlyMemory<byte> Written => output.WrittenMemory;

    /// <summary>Encodes one value.</summary>
    /// <param name="value">The value, as <see cref="WriteValue"/> takes it.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is of a type AMQP 1.0 has no encoding for here.</exception>
    public static byte[] Encode(object? value) => EncodeEach([value]);

    private static byte[] EncodeEach(IEnumerable<object?> values)
    {
        var encoder = new Amqp1Encoder();
        foreach (var value in values)
        {
            encoder.WriteValue(value);
        }

        return encoder.Written.ToArray();
    }

    /// <summary>The format code <see cref="WriteValue"/> writes <paramref name="value"/> with.</summary>
    /// <param name="value">The value; not an <see cref="Amqp1Described"/>, which has no format code of its own.</param>
    /// <returns>The format code, from <see cref="Amqp1FormatCode"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is of a type AMQP 1.0 has no encoding for here.</exception>
    public static byte FormatCodeFor(object? value)
    {
        if (value is null)
        {
            return Amqp1FormatCode.Null;
        }

        if (FormatCodeChoosers.TryGetValue(value.GetType(), out var choose))
        {
            return choose(value);
        }

        return value is IReadOnlyList<object?> list
            ? ListFormatCode(list)
            : throw new ArgumentException($"A {value.GetType()} has no AMQP 1.0 encoding.", nameof(value));
    }

    /// <summary>Writes one value: its constructor, then its data.</summary>
    /// <param name="value">The value, as <see cref="Amqp1Decoder"/> reads it.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is of a type AMQP 1.0 has no encoding for here.</exception>
    public void WriteValue(object? value)
    {
        if (value is Amqp1Described described)
        {
            WriteOctet(Amqp1FormatCode.Described);
            WriteValue(described.Descriptor);
            WriteValue(described.Value);
            return;
        }

        var formatCode = FormatCodeFor(value);
        WriteOctet(formatCode);
        WriteBody(formatCode, value);
    }

    private static byte UnsignedFormatCode(ulong value, byte zero, byte small, byte wide) =>
        value == 0 ? zero : value <= byte.MaxValue ? small : wide;

    private static byte SignedFormatCode(long value, byte small, byte wide) =>
        value is >= sbyte.MinValue and <= sbyte.MaxValue ? small : wide;

    private static byte VariableWidthFormatCode(int length, byte narrow, byte wide) =>
        length <= byte.MaxValue ? narrow : wide;

    // A one-octet size covers the one-octet count and the content.
    private static byte CompositeFormatCode(int count, int contentLength, byte narrow, byte wide) =>
        count <= byte.MaxValue && contentLength + 1 <= byte.MaxValue ? narrow : wide;

    private static byte ListFormatCode(IReadOnlyList<object?> list) =>
        list.Count == 0
            ? Amqp1FormatCode.List0
            : CompositeFormatCode(list.Count, EncodeEach(list).Length, Amqp1FormatCode.List8, Amqp1FormatCode.List32);

    private static int MapCount(Amqp1Map map) => map.Entries.Count * 2;

    private static byte[] MapContent(Amqp1Map map) => EncodeEach(map.Entries.SelectMany(entry => new[] { entry.Key, entry.Value }));

    private static byte[] ArrayContent(Amqp1Array array)
    {
        var encoder = new Amqp1Encoder();
        if (array.ElementDescriptor is not null)
        {
            encoder.WriteOctet(Amqp1FormatCode.Described);
            encoder.WriteValue(array.ElementDescriptor);
        }

        encoder.WriteOctet(array.ElementFormatCode);
        foreach (var element in array.Elements)
        {
            encoder.WriteBody(array.ElementFormatCode, element);
        }

        return encoder.Written.ToArray();
    }

    private static byte[] SymbolBytes(Amqp1Symbol symbol) =>
        Ascii.IsValid(symbol.Value)
            ? Encoding.ASCII.GetBytes(symbol.Value)
            : throw new ArgumentException($"The symbol '{symbol.Value}' is not ASCII.", nameof(symbol));

    private void WriteBody(byte formatCode, object? value)
    {
        if (!BodyWriters.TryGetValue(formatCode, out var writeBody))
        {
            throw new ArgumentException($"Format code 0x{formatCode:X2} is not one AMQP 1.0 defines.", nameof(formatCode));
        }

        writeBody(this, value);
    }

    private void WriteComposite(int width, int count, byte[] content)
    {
        WriteUnsigned(width, checked((uint)(width + content.Length)));
        WriteUnsigned(width, (uint)count);
        WriteOctets(content);
    }

    private void WriteSized(int width, byte[] content)
    {
        WriteUnsigned(width, (uint)content.Length);
        WriteOctets(content);
    }

    private void WriteUnsigned(int width, uint value)
    {
        if (width == 1)
        {
            WriteOctet(checked((byte)value));
            return;
        }

        BinaryPrimitives.WriteUInt32BigEndian(Grow(4), value);
    }

    private void WriteOctet(byte value) => Grow(1)[0] = value;

    private void WriteOctets(ReadOnlySpan<byte> values) => output.Write(values);

    private Span<byte> Grow(int count)
    {
        var span = output.GetSpan(count)[..count];
        output.Advance(count);
        return span;
    }
}
