using System.Buffers.Binary;
using System.Text;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Reads AMQP 1.0 values (Part 1 sections 1.2 to 1.6) from a buffer, one after another, in
/// every encoding the specification allows: each width of each primitive type, described values,
/// lists, maps and arrays.
/// </summary>
/// <remarks>
/// <para>
/// A value is returned as the .NET type that holds it: <see langword="null"/>, <see cref="bool"/>,
/// <see cref="byte"/> (<c>ubyte</c>), <see cref="sbyte"/> (<c>byte</c>), <see cref="ushort"/>,
/// <see cref="short"/>, <see cref="uint"/>, <see cref="int"/>, <see cref="ulong"/>,
/// <see cref="long"/>, <see cref="float"/>, <see cref="double"/>, <see cref="Amqp1Decimal32"/>,
/// <see cref="Amqp1Decimal64"/>, <see cref="Amqp1Decimal128"/>, <see cref="Rune"/> (<c>char</c>),
/// <see cref="Amqp1Timestamp"/>, <see cref="Guid"/> (<c>uuid</c>), a <see cref="byte"/> array
/// (<c>binary</c>), <see cref="string"/>, <see cref="Amqp1Symbol"/>, an
/// <see cref="IReadOnlyList{T}"/> of values (<c>list</c>), <see cref="Amqp1Map"/>,
/// <see cref="Amqp1Array"/> and <see cref="Amqp1Described"/>. Every width of a type reads as the
/// same .NET type: <c>uint0</c>, <c>smalluint</c> and <c>uint</c> are all a <see cref="uint"/>.
/// </para>
/// <para>
/// Anything that does not decode throws <see cref="Amqp1DecodeException"/>: an unknown format
/// code, a value past the end of the buffer, a list, map or array whose elements do not fill
/// exactly its size, a map with an odd count, a <c>boolean</c> octet other than 0 or 1, a
/// <c>char</c> that is not a Unicode scalar value, a <c>string</c> that is not well-formed UTF-8,
/// a <c>symbol</c> that is not ASCII, or values nested deeper than <see cref="MaxNestingDepth"/>.
/// Not safe for concurrent calls.
/// </para>
/// </remarks>
/// <param name="bytes">The buffer to read from, starting at its first byte.</param>
public sealed class Amqp1Decoder(ReadOnlyMemory<byte> bytes)
{
    /// <summary>How deep described values, lists, maps and arrays may nest inside each other: 64.</summary>
    public const int MaxNestingDepth = 64;

    /// <summary>
    /// The most elements an array of a zero-width element constructor (<c>null</c>,
    /// <c>true</c>, <c>false</c>, <c>uint0</c>, <c>ulong0</c>, <c>list0</c>) may claim: 65536, so a
    /// few bytes cannot ask for billions of elements.
    /// </summary>
    public const int MaxZeroWidthArrayElements = 65536;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly HashSet<byte> ZeroWidthFormatCodes =
    [
        Amqp1FormatCode.Null, Amqp1FormatCode.BooleanTrue, Amqp1FormatCode.BooleanFalse,
        Amqp1FormatCode.UInt0, Amqp1FormatCode.ULong0, Amqp1FormatCode.List0,
    ];

    private static readonly Dictionary<byte, Func<Amqp1Decoder, object?>> BodyReaders = new()
    {
        [Amqp1FormatCode.Null] = _ => null,
        [Amqp1FormatCode.BooleanTrue] = _ => true,
        [Amqp1FormatCode.BooleanFalse] = _ => false,
        [Amqp1FormatCode.UInt0] = _ => 0u,
        [Amqp1FormatCode.ULong0] = _ => 0ul,
        [Amqp1FormatCode.List0] = _ => Array.Empty<object?>(),
        [Amqp1FormatCode.UByte] = decoder => decoder.ReadOctet(),
        [Amqp1FormatCode.Byte] = decoder => (sbyte)decoder.ReadOctet(),
        [Amqp1FormatCode.SmallUInt] = decoder => (uint)decoder.ReadOctet(),
        [Amqp1FormatCode.SmallULong] = decoder => (ulong)decoder.ReadOctet(),
        [Amqp1FormatCode.SmallInt] = decoder => (int)(sbyte)decoder.ReadOctet(),
        [Amqp1FormatCode.SmallLong] = decoder => (long)(sbyte)decoder.ReadOctet(),
        [Amqp1FormatCode.Boolean] = decoder => decoder.ReadBooleanOctet(),
        [Amqp1FormatCode.UShort] = decoder => BinaryPrimitives.ReadUInt16BigEndian(decoder.Take(2)),
        [Amqp1FormatCode.Short] = decoder => BinaryPrimitives.ReadInt16BigEndian(decoder.Take(2)),
        [Amqp1FormatCode.UInt] = decoder => BinaryPrimitives.ReadUInt32BigEndian(decoder.Take(4)),
        [Amqp1FormatCode.Int] = decoder => BinaryPrimitives.ReadInt32BigEndian(decoder.Take(4)),
        [Amqp1FormatCode.Float] = decoder => BinaryPrimitives.ReadSingleBigEndian(decoder.Take(4)),
        [Amqp1FormatCode.Char] = decoder => decoder.ReadChar(),
        [Amqp1FormatCode.Decimal32] = decoder => new Amqp1Decimal32(BinaryPrimitives.ReadUInt32BigEndian(decoder.Take(4))),
        [Amqp1FormatCode.ULong] = decoder => BinaryPrimitives.ReadUInt64BigEndian(decoder.Take(8)),
        [Amqp1FormatCode.Long] = decoder => BinaryPrimitives.ReadInt64BigEndian(decoder.Take(8)),
        [Amqp1FormatCode.Double] = decoder => BinaryPrimitives.ReadDoubleBigEndian(decoder.Take(8)),
        [Amqp1FormatCode.Timestamp] = decoder => new Amqp1Timestamp(BinaryPrimitives.ReadInt64BigEndian(decoder.Take(8))),
        [Amqp1FormatCode.Decimal64] = decoder => new Amqp1Decimal64(BinaryPrimitives.ReadUInt64BigEndian(decoder.Take(8))),
        [Amqp1FormatCode.Decimal128] = decoder => new Amqp1Decimal128(BinaryPrimitives.ReadUInt128BigEndian(decoder.Take(16))),
        [Amqp1FormatCode.Uuid] = decoder => new Guid(decoder.Take(16), bigEndian: true),
        [Amqp1FormatCode.Binary8] = decoder => decoder.Take(decoder.ReadLength(1)).ToArray(),
        [Amqp1FormatCode.Binary32] = decoder => decoder.Take(decoder.ReadLength(4)).ToArray(),
        [Amqp1FormatCode.String8] = decoder => decoder.ReadUtf8(decoder.ReadLength(1)),
        [Amqp1FormatCode.String32] = decoder => decoder.ReadUtf8(decoder.ReadLength(4)),
        [Amqp1FormatCode.Symbol8] = decoder => decoder.ReadSymbol(decoder.ReadLength(1)),
        [Amqp1FormatCode.Symbol32] = decoder => decoder.ReadSymbol(decoder.ReadLength(4)),
        [Amqp1FormatCode.List8] = decoder => decoder.Nested(() => decoder.ReadList(1)),
        [Amqp1FormatCode.List32] = decoder => decoder.Nested(() => decoder.ReadList(4)),
        [Amqp1FormatCode.Map8] = decoder => decoder.Nested(() => decoder.ReadMap(1)),
        [Amqp1FormatCode.Map32] = decoder => decoder.Nested(() => decoder.ReadMap(4)),
        [Amqp1FormatCode.Array8] = decoder => decoder.Nested(() => decoder.ReadArray(1)),
        [Amqp1FormatCode.Array32] = decoder => decoder.Nested(() => decoder.ReadArray(4)),
    };

    private int position;
    private int depth;

    /// <summary>How many bytes have been read.</summary>
    public int Position => position;

    /// <summary>How many bytes are left to read.</summary>
    public int Remaining => bytes.Length - position;

    /// <summary>Reads one value: its constructor, then the data the constructor says.</summary>
    /// <returns>The value, as the remarks on <see cref="Amqp1Decoder"/> list.</returns>
    /// <exception cref="Amqp1DecodeException">The bytes do not decode as one AMQP 1.0 value.</exception>
    public object? ReadValue()
    {
        var formatCode = ReadOctet();
        return formatCode == Amqp1FormatCode.Described
            ? Nested(() => new Amqp1Described(ReadValue(), ReadValue()))
            : BodyReaderFor(formatCode)(this);
    }

    private static Func<Amqp1Decoder, object?> BodyReaderFor(byte formatCode) =>
        BodyReaders.TryGetValue(formatCode, out var readBody)
            ? readBody
            : throw new Amqp1DecodeException($"Format code 0x{formatCode:X2} is not one AMQP 1.0 defines.");

    private T Nested<T>(Func<T> read)
    {
        if (depth == MaxNestingDepth)
        {
            throw new Amqp1DecodeException($"Values nest deeper than {MaxNestingDepth}.");
        }

        depth++;
        try
        {
            return read();
        }
        finally
        {
            depth--;
        }
    }

    private IReadOnlyList<object?> ReadList(int width)
    {
        var end = ReadRegionEnd(width);
        var elements = new object?[ReadCount(width, end - position - width)];
        for (var index = 0; index < elements.Length; index++)
        {
            elements[index] = ReadValue();
        }

        ExpectRegionEnd(end);
        return elements;
    }

    private Amqp1Map ReadMap(int width)
    {
        var end = ReadRegionEnd(width);
        var count = ReadCount(width, end - position - width);
        if (count % 2 != 0)
        {
            throw new Amqp1DecodeException("A map has an odd count of keys and values.");
        }

        var entries = new KeyValuePair<object?, object?>[count / 2];
        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = new(ReadValue(), ReadValue());
        }

        ExpectRegionEnd(end);
        return new(entries);
    }

    private Amqp1Array ReadArray(int width)
    {
        var end = ReadRegionEnd(width);
        var count = ReadCount(width, int.MaxValue);
        var descriptor = PeekOctet() == Amqp1FormatCode.Described ? ReadArrayDescriptor() : null;
        var formatCode = ReadOctet();
        var readElement = BodyReaderFor(formatCode);
        var mostElements = ZeroWidthFormatCodes.Contains(formatCode) ? MaxZeroWidthArrayElements : end - position;
        if (count > mostElements)
        {
            throw new Amqp1DecodeException("An array claims more elements than its size can hold.");
        }

        var elements = new object?[count];
        for (var index = 0; index < elements.Length; index++)
        {
            elements[index] = readElement(this);
        }

        ExpectRegionEnd(end);
        return new(formatCode, elements, descriptor);
    }

    private object? ReadArrayDescriptor()
    {
        position++;
        return ReadValue();
    }

    // Reads a size field and returns the position the region it covers ends at.
    private int ReadRegionEnd(int width)
    {
        var size = ReadLength(width);
        return position + size;
    }

    // Reads a count field; a count above mostElements (one byte per element at least) is refused.
    private int ReadCount(int width, int mostElements)
    {
        var count = ReadUnsigned(width);
        if (count > (uint)Math.Max(mostElements, 0))
        {
            throw new Amqp1DecodeException("A list, map or array claims more elements than its size can hold.");
        }

        return (int)count;
    }

    private void ExpectRegionEnd(int end)
    {
        if (position != end)
        {
            throw new Amqp1DecodeException("The elements of a list, map or array do not fill exactly its size.");
        }
    }

    private int ReadLength(int width)
    {
        var length = ReadUnsigned(width);
        if (length > (uint)Remaining)
        {
            throw new Amqp1DecodeException("A size or length runs past the end of the bytes.");
        }

        return (int)length;
    }

    private uint ReadUnsigned(int width) => width == 1 ? ReadOctet() : BinaryPrimitives.ReadUInt32BigEndian(Take(4));

    private bool ReadBooleanOctet() => ReadOctet() switch
    {
        0x00 => false,
        0x01 => true,
        _ => throw new Amqp1DecodeException("A boolean octet is neither 0x00 nor 0x01."),
    };

    private Rune ReadChar() =>
        Rune.TryCreate(BinaryPrimitives.ReadUInt32BigEndian(Take(4)), out var rune)
            ? rune
            : throw new Amqp1DecodeException("A char is not a Unicode scalar value.");

    private string ReadUtf8(int length)
    {
        var text = Take(length);
        try
        {
            return StrictUtf8.GetString(text);
        }
        catch (DecoderFallbackException exception)
        {
            throw new Amqp1DecodeException("A string is not well-formed UTF-8.", exception);
        }
    }

    private Amqp1Symbol ReadSymbol(int length)
    {
        var text = Take(length);
        return Ascii.IsValid(text)
            ? new Amqp1Symbol(Encoding.ASCII.GetString(text))
            : throw new Amqp1DecodeException("A symbol is not ASCII.");
    }

    private byte PeekOctet()
    {
        var octet = ReadOctet();
        position--;
        return octet;
    }

    private byte ReadOctet() => Take(1)[0];

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count > Remaining)
        {
            throw new Amqp1DecodeException("A value runs past the end of the bytes.");
        }

        var span = bytes.Span.Slice(position, count);
        position += count;
        return span;
    }
}
