using System.Buffers.Binary;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Reads a serialized protobuf message field by field, as the protobuf encoding guide lays it out: each field
/// is a varint tag (field number and wire type) and its value. It knows no message type; a field a type does
/// not know is read whole all the same, so it can be skipped or kept, and a deprecated group is read to its
/// matching end so it can be skipped too.
/// </summary>
public sealed class ProtobufReader
{
    /// <summary>The largest field number the guide allows, 2^29 - 1.</summary>
    public const int MaximumFieldNumber = (1 << 29) - 1;

    /// <summary>How deeply groups may nest, protobuf's own default recursion limit.</summary>
    public const int MaximumGroupDepth = 100;

    private const int MaximumVarintLength = 10;

    private readonly ReadOnlyMemory<byte> bytes;
    private int position;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufReader" /> class.
    /// </summary>
    /// <param name="bytes">The serialized message.</param>
    public ProtobufReader(ReadOnlyMemory<byte> bytes)
    {
        this.bytes = bytes;
    }

    /// <summary>Whether every byte has been read.</summary>
    public bool IsAtEnd => position == bytes.Length;

    /// <summary>
    /// Reads the next field.
    /// </summary>
    /// <param name="field">The field, or the default when the message has ended.</param>
    /// <returns><see langword="true" /> when a field was read; <see langword="false" /> at the end of the message.</returns>
    /// <exception cref="ProtobufFormatException">The bytes are not a field.</exception>
    public bool TryReadField(out ProtobufField field)
    {
        field = default;
        if (IsAtEnd)
        {
            return false;
        }

        field = ReadField(groupDepth: 0);
        if (field.WireType == ProtobufWireType.EndGroup)
        {
            throw new ProtobufFormatException($"Field {field.FieldNumber} ends a group that never started.");
        }

        return true;
    }

    /// <summary>
    /// Reads one base 128 varint: up to 10 bytes, each giving 7 bits, least significant first, with the high
    /// bit set on every byte but the last.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtobufFormatException">The message ends inside the varint, or it runs past 10 bytes.</exception>
    public ulong ReadVarint()
    {
        ulong value = 0;
        for (var index = 0; index < MaximumVarintLength; index++)
        {
            var next = ReadByte();
            value |= (ulong)(next & 0x7F) << (7 * index);
            if (next < 0x80)
            {
                return value;
            }
        }

        throw new ProtobufFormatException("A varint runs past 10 bytes.");
    }

    private ProtobufField ReadField(int groupDepth)
    {
        var begin = position;
        var tag = ReadVarint();
        return ReadFieldBody(begin, ReadFieldNumber(tag), tag, groupDepth);
    }

    private static int ReadFieldNumber(ulong tag)
    {
        var fieldNumber = tag >> 3;
        if (fieldNumber is 0 or > MaximumFieldNumber)
        {
            throw new ProtobufFormatException($"A tag names field number {fieldNumber}.");
        }

        return (int)fieldNumber;
    }

    private ProtobufField ReadFieldBody(int begin, int number, ulong tag, int groupDepth)
    {
        return (ProtobufWireType)(tag & 7) switch
        {
            ProtobufWireType.Varint => Scalar(begin, number, ProtobufWireType.Varint, ReadVarint()),
            ProtobufWireType.Fixed64 => Scalar(begin, number, ProtobufWireType.Fixed64, BinaryPrimitives.ReadUInt64LittleEndian(Take(8).Span)),
            ProtobufWireType.LengthDelimited => ReadLengthDelimited(begin, number),
            ProtobufWireType.StartGroup => ReadGroup(begin, number, groupDepth + 1),
            ProtobufWireType.EndGroup => Scalar(begin, number, ProtobufWireType.EndGroup, 0),
            ProtobufWireType.Fixed32 => Scalar(begin, number, ProtobufWireType.Fixed32, BinaryPrimitives.ReadUInt32LittleEndian(Take(4).Span)),
            _ => throw new ProtobufFormatException($"Field {number} has wire type {tag & 7}, which is not defined."),
        };
    }

    private ProtobufField ReadLengthDelimited(int begin, int number)
    {
        var length = ReadVarint();
        if (length > (ulong)(bytes.Length - position))
        {
            throw Truncated();
        }

        var content = Take((int)length);
        return new ProtobufField(number, ProtobufWireType.LengthDelimited, 0, content, bytes[begin..position]);
    }

    private ProtobufField ReadGroup(int begin, int number, int groupDepth)
    {
        if (groupDepth > MaximumGroupDepth)
        {
            throw new ProtobufFormatException($"Groups nest deeper than {MaximumGroupDepth}.");
        }

        var contentStart = position;
        while (true)
        {
            var contentEnd = position;
            var inner = ReadField(groupDepth);
            if (inner.WireType == ProtobufWireType.EndGroup)
            {
                return inner.FieldNumber == number
                    ? new ProtobufField(number, ProtobufWireType.StartGroup, 0, bytes[contentStart..contentEnd], bytes[begin..position])
                    : throw new ProtobufFormatException($"Group {number} ends with field {inner.FieldNumber}.");
            }
        }
    }

    private ProtobufField Scalar(int begin, int number, ProtobufWireType wireType, ulong value) =>
        new(number, wireType, value, ReadOnlyMemory<byte>.Empty, bytes[begin..position]);

    private byte ReadByte() => Take(1).Span[0];

    private ReadOnlyMemory<byte> Take(int length)
    {
        if (bytes.Length - position < length)
        {
            throw Truncated();
        }

        var taken = bytes.Slice(position, length);
        position += length;
        return taken;
    }

    private static ProtobufFormatException Truncated() => new("The message ends inside a field.");
}
