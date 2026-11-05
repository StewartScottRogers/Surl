using System.Text;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// One field as <see cref="ProtobufReader" /> read it. A message type reads the fields it knows with the
/// accessor for their type, which refuses the wrong wire type, and keeps the rest by writing
/// <see cref="Encoded" /> back with <see cref="ProtobufWriter.WriteEncodedField" />.
/// </summary>
/// <param name="FieldNumber">The field number, 1 to 536870911.</param>
/// <param name="WireType">The wire type its tag named.</param>
/// <param name="Scalar">The value of a <c>VARINT</c>, <c>I64</c> or <c>I32</c> field; 0 otherwise.</param>
/// <param name="Content">The bytes of a <c>LEN</c> field, or the fields inside a group; empty otherwise.</param>
/// <param name="Encoded">The whole field as it was read, tag included.</param>
public readonly record struct ProtobufField(
    int FieldNumber,
    ProtobufWireType WireType,
    ulong Scalar,
    ReadOnlyMemory<byte> Content,
    ReadOnlyMemory<byte> Encoded)
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Reads a <c>uint64</c>.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtobufFormatException">The field is not <c>VARINT</c>.</exception>
    public ulong ReadUInt64() => Expect(ProtobufWireType.Varint).Scalar;

    /// <summary>Reads a <c>uint32</c>, keeping the low 32 bits as the guide says a parser does.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtobufFormatException">The field is not <c>VARINT</c>.</exception>
    public uint ReadUInt32() => (uint)ReadUInt64();

    /// <summary>Reads an <c>int64</c> (two's complement).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtobufFormatException">The field is not <c>VARINT</c>.</exception>
    public long ReadInt64() => (long)ReadUInt64();

    /// <summary>Reads an <c>int32</c> or an enum, keeping the low 32 bits.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtobufFormatException">The field is not <c>VARINT</c>.</exception>
    public int ReadInt32() => (int)ReadUInt64();

    /// <summary>Reads a <c>sint32</c> (ZigZag).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtobufFormatException">The field is not <c>VARINT</c>.</exception>
    public int ReadSInt32() => ProtobufZigZag.Decode32(ReadUInt32());

    /// <summary>Reads a <c>sint64</c> (ZigZag).</summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtobufFormatException">The field is not <c>VARINT</c>.</exception>
    public long ReadSInt64() => ProtobufZigZag.Decode64(ReadUInt64());

    /// <summary>Reads a <c>bool</c>: any value other than 0 is true.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtobufFormatException">The field is not <c>VARINT</c>.</exception>
    public bool ReadBool() => ReadUInt64() != 0;

    /// <summary>Reads a <c>fixed64</c>; <c>sfixed64</c> and <c>double</c> are its bits reinterpreted.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtobufFormatException">The field is not <c>I64</c>.</exception>
    public ulong ReadFixed64() => Expect(ProtobufWireType.Fixed64).Scalar;

    /// <summary>Reads a <c>fixed32</c>; <c>sfixed32</c> and <c>float</c> are its bits reinterpreted.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="ProtobufFormatException">The field is not <c>I32</c>.</exception>
    public uint ReadFixed32() => (uint)Expect(ProtobufWireType.Fixed32).Scalar;

    /// <summary>Reads a <c>bytes</c> field.</summary>
    /// <returns>The bytes.</returns>
    /// <exception cref="ProtobufFormatException">The field is not <c>LEN</c>.</exception>
    public ReadOnlyMemory<byte> ReadBytes() => Expect(ProtobufWireType.LengthDelimited).Content;

    /// <summary>Reads a <c>string</c> field, which must be UTF-8.</summary>
    /// <returns>The text.</returns>
    /// <exception cref="ProtobufFormatException">The field is not <c>LEN</c>, or its bytes are not UTF-8.</exception>
    public string ReadString()
    {
        var bytes = ReadBytes();
        try
        {
            return StrictUtf8.GetString(bytes.Span);
        }
        catch (DecoderFallbackException failure)
        {
            throw new ProtobufFormatException($"Field {FieldNumber} is not UTF-8.", failure);
        }
    }

    /// <summary>Reads an embedded message.</summary>
    /// <returns>A reader over the message's fields.</returns>
    /// <exception cref="ProtobufFormatException">The field is not <c>LEN</c>.</exception>
    public ProtobufReader ReadMessage() => new(ReadBytes());

    /// <summary>
    /// Reads a repeated varint field, packed (one <c>LEN</c> field of varints) or not (one <c>VARINT</c> field),
    /// since the guide says a parser must accept both.
    /// </summary>
    /// <returns>The values this field holds, in order.</returns>
    /// <exception cref="ProtobufFormatException">The field is neither, or a packed varint is truncated.</exception>
    public IReadOnlyList<ulong> ReadRepeatedVarints()
    {
        if (WireType == ProtobufWireType.Varint)
        {
            return [Scalar];
        }

        var packed = new ProtobufReader(ReadBytes());
        var values = new List<ulong>();
        while (!packed.IsAtEnd)
        {
            values.Add(packed.ReadVarint());
        }

        return values;
    }

    private ProtobufField Expect(ProtobufWireType wireType) => WireType == wireType
        ? this
        : throw new ProtobufFormatException($"Field {FieldNumber} has wire type {(int)WireType}, not {(int)wireType}.");
}
