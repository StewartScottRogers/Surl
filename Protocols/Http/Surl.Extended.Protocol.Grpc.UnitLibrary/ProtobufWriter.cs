using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Writes a serialized protobuf message field by field, as the protobuf encoding guide lays it out. Fields
/// are written in the order the caller writes them; a type writes its fields in field-number order, then any
/// unknown fields it kept.
/// </summary>
public sealed class ProtobufWriter
{
    private readonly ArrayBufferWriter<byte> written = new();

    /// <summary>The message written so far.</summary>
    /// <returns>A copy of its bytes.</returns>
    public byte[] ToArray() => written.WrittenSpan.ToArray();

    /// <summary>Writes a <c>uint64</c>, or any value already a varint's bits.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The value.</param>
    public void WriteUInt64(int fieldNumber, ulong value)
    {
        WriteTag(fieldNumber, ProtobufWireType.Varint);
        WriteVarint(value);
    }

    /// <summary>Writes a <c>uint32</c>.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The value.</param>
    public void WriteUInt32(int fieldNumber, uint value) => WriteUInt64(fieldNumber, value);

    /// <summary>Writes an <c>int64</c>; a negative value takes 10 bytes.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The value.</param>
    public void WriteInt64(int fieldNumber, long value) => WriteUInt64(fieldNumber, (ulong)value);

    /// <summary>Writes an <c>int32</c> or an enum; a negative value is sign-extended to 10 bytes, as the guide says.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The value.</param>
    public void WriteInt32(int fieldNumber, int value) => WriteInt64(fieldNumber, value);

    /// <summary>Writes a <c>sint32</c> (ZigZag).</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The value.</param>
    public void WriteSInt32(int fieldNumber, int value) => WriteUInt64(fieldNumber, ProtobufZigZag.Encode32(value));

    /// <summary>Writes a <c>sint64</c> (ZigZag).</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The value.</param>
    public void WriteSInt64(int fieldNumber, long value) => WriteUInt64(fieldNumber, ProtobufZigZag.Encode64(value));

    /// <summary>Writes a <c>bool</c> as 0 or 1.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The value.</param>
    public void WriteBool(int fieldNumber, bool value) => WriteUInt64(fieldNumber, value ? 1UL : 0UL);

    /// <summary>Writes a <c>fixed64</c>, 8 bytes little-endian.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The value.</param>
    public void WriteFixed64(int fieldNumber, ulong value)
    {
        WriteTag(fieldNumber, ProtobufWireType.Fixed64);
        BinaryPrimitives.WriteUInt64LittleEndian(written.GetSpan(8), value);
        written.Advance(8);
    }

    /// <summary>Writes a <c>fixed32</c>, 4 bytes little-endian.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The value.</param>
    public void WriteFixed32(int fieldNumber, uint value)
    {
        WriteTag(fieldNumber, ProtobufWireType.Fixed32);
        BinaryPrimitives.WriteUInt32LittleEndian(written.GetSpan(4), value);
        written.Advance(4);
    }

    /// <summary>Writes a <c>bytes</c> field, or an embedded message already serialized.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The bytes.</param>
    public void WriteBytes(int fieldNumber, ReadOnlySpan<byte> value)
    {
        WriteTag(fieldNumber, ProtobufWireType.LengthDelimited);
        WriteVarint((ulong)value.Length);
        written.Write(value);
    }

    /// <summary>Writes a <c>string</c> field as UTF-8.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="value">The text.</param>
    public void WriteString(int fieldNumber, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteBytes(fieldNumber, Encoding.UTF8.GetBytes(value));
    }

    /// <summary>Writes an embedded message.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="message">The embedded message's writer.</param>
    public void WriteMessage(int fieldNumber, ProtobufWriter message)
    {
        ArgumentNullException.ThrowIfNull(message);
        WriteBytes(fieldNumber, message.written.WrittenSpan);
    }

    /// <summary>Writes a packed repeated varint field: one <c>LEN</c> field holding every value's varint.</summary>
    /// <param name="fieldNumber">The field number.</param>
    /// <param name="values">The values, in order.</param>
    public void WritePackedVarints(int fieldNumber, IEnumerable<ulong> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var packed = new ProtobufWriter();
        foreach (var value in values)
        {
            packed.WriteVarint(value);
        }

        WriteBytes(fieldNumber, packed.written.WrittenSpan);
    }

    /// <summary>Writes a field back exactly as it was read, so an unknown field is kept.</summary>
    /// <param name="field">The field as <see cref="ProtobufReader" /> read it.</param>
    public void WriteEncodedField(ProtobufField field) => written.Write(field.Encoded.Span);

    private void WriteTag(int fieldNumber, ProtobufWireType wireType)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fieldNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fieldNumber, ProtobufReader.MaximumFieldNumber);
        WriteVarint(((ulong)fieldNumber << 3) | (ulong)wireType);
    }

    private void WriteVarint(ulong value)
    {
        while (value >= 0x80)
        {
            written.Write([(byte)(value | 0x80)]);
            value >>= 7;
        }

        written.Write([(byte)value]);
    }
}
