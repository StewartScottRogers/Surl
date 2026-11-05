using System.Buffers.Binary;
using System.Text;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Reads the data representations of MQTT 5.0 section 1.5 and property lists (section 2.2.2)
/// from a packet's body, front to back. Every read past the end, and every value MQTT 5.0
/// forbids, throws <see cref="Mqtt5PacketFaultException"/> with the reason code section 4.13
/// names.
/// </summary>
/// <param name="body">The bytes to read: a packet's body, or a property list within one.</param>
internal sealed class Mqtt5BodyReader(ReadOnlyMemory<byte> body)
{
    /// <summary>
    /// How many bytes a Variable Byte Integer may take (section 1.5.5).
    /// </summary>
    public const int MaxVariableByteIntegerBytes = 4;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private int position;

    /// <summary>
    /// How many bytes are left to read.
    /// </summary>
    public int Remaining => body.Length - position;

    /// <summary>
    /// Reads one byte (section 1.5.1).
    /// </summary>
    /// <returns>The byte.</returns>
    public byte ReadByte() => Take(1).Span[0];

    /// <summary>
    /// Reads a big-endian 16-bit integer (section 1.5.2).
    /// </summary>
    /// <returns>The integer.</returns>
    public ushort ReadTwoByteInteger() => BinaryPrimitives.ReadUInt16BigEndian(Take(2).Span);

    /// <summary>
    /// Reads a big-endian 32-bit integer (section 1.5.3).
    /// </summary>
    /// <returns>The integer.</returns>
    public uint ReadFourByteInteger() => BinaryPrimitives.ReadUInt32BigEndian(Take(4).Span);

    /// <summary>
    /// Reads a Variable Byte Integer (section 1.5.5): at most four bytes, in the fewest bytes
    /// that hold the value (<c>[MQTT-1.5.5-1]</c>).
    /// </summary>
    /// <returns>The integer, 0 to 268435455.</returns>
    public int ReadVariableByteInteger()
    {
        var value = 0;
        for (var index = 0; index < MaxVariableByteIntegerBytes; index++)
        {
            var encodedByte = ReadByte();
            value |= (encodedByte & 0x7F) << (7 * index);
            if ((encodedByte & 0x80) == 0)
            {
                return IsNotMinimal(index, encodedByte)
                    ? throw Fault(Mqtt5PacketFault.Malformed("[MQTT-1.5.5-1]"))
                    : value;
            }
        }

        throw Fault(Mqtt5PacketFault.Malformed("1.5.5"));
    }

    /// <summary>
    /// Whether the last byte of a Variable Byte Integer makes it longer than it needs to be:
    /// a final byte of 0 after the first adds nothing.
    /// </summary>
    /// <param name="index">The byte's place, from 0.</param>
    /// <param name="encodedByte">The byte, its continuation bit clear.</param>
    /// <returns><see langword="true"/> when the encoding is not minimal.</returns>
    public static bool IsNotMinimal(int index, int encodedByte) => index > 0 && encodedByte == 0;

    /// <summary>
    /// Reads a UTF-8 Encoded String (section 1.5.4): well-formed UTF-8 with no surrogate code
    /// point (<c>[MQTT-1.5.4-1]</c>) and no U+0000 (<c>[MQTT-1.5.4-2]</c>). A byte order mark
    /// is kept, not stripped (<c>[MQTT-1.5.4-3]</c>).
    /// </summary>
    /// <returns>The string.</returns>
    public string ReadUtf8String()
    {
        var bytes = Take(ReadTwoByteInteger());
        string text;
        try
        {
            text = StrictUtf8.GetString(bytes.Span);
        }
        catch (DecoderFallbackException)
        {
            throw Fault(Mqtt5PacketFault.Malformed("[MQTT-1.5.4-1]"));
        }

        return text.Contains('\0', StringComparison.Ordinal)
            ? throw Fault(Mqtt5PacketFault.Malformed("[MQTT-1.5.4-2]"))
            : text;
    }

    /// <summary>
    /// Reads Binary Data (section 1.5.6): a two-byte length, then that many bytes.
    /// </summary>
    /// <returns>The bytes.</returns>
    public byte[] ReadBinaryData() => Take(ReadTwoByteInteger()).ToArray();

    /// <summary>
    /// Reads every byte left, as a <c>PUBLISH</c> payload is (section 3.3.3).
    /// </summary>
    /// <returns>The bytes.</returns>
    public byte[] ReadRemaining() => Take(Remaining).ToArray();

    /// <summary>
    /// Reads a Packet Identifier (section 2.2.1), which is never 0 (<c>[MQTT-2.2.1-3]</c>,
    /// a Protocol Error).
    /// </summary>
    /// <returns>The identifier.</returns>
    public ushort ReadPacketIdentifier()
    {
        var identifier = ReadTwoByteInteger();
        return identifier == 0 ? throw Fault(Mqtt5PacketFault.ProtocolError("[MQTT-2.2.1-3]")) : identifier;
    }

    /// <summary>
    /// Reads a property list (section 2.2.2): its Variable Byte Integer length, then each
    /// property. An identifier table 2-4 does not define, or does not allow in
    /// <paramref name="context"/>, is a Malformed Packet (section 2.2.2.2); a property there
    /// twice that may appear only once is a Protocol Error.
    /// </summary>
    /// <param name="context">Where the list stands.</param>
    /// <returns>The properties, in the order read.</returns>
    public IReadOnlyList<Mqtt5Property> ReadProperties(Mqtt5PropertyContext context)
    {
        var list = new Mqtt5BodyReader(Take(ReadVariableByteInteger()));
        var properties = new List<Mqtt5Property>();
        var seen = new HashSet<int>();
        while (list.Remaining > 0)
        {
            var identifier = list.ReadVariableByteInteger();
            if (!Mqtt5PropertyDefinition.TryFind(identifier, out var definition) || !definition.IsAllowedIn(context))
            {
                throw Fault(Mqtt5PacketFault.Malformed("2.2.2.2"));
            }

            if (!seen.Add(identifier) && !definition.IsRepeatableIn(context))
            {
                throw Fault(Mqtt5PacketFault.ProtocolError("2.2.2.2"));
            }

            properties.Add(list.ReadPropertyValue(definition));
        }

        return properties;
    }

    /// <summary>
    /// Reads the tail of a packet whose Reason Code and Property Length may be left off
    /// (sections 3.4.2.1, 3.14.2.1, 3.15.2.1 and their peers): no byte left means Success with
    /// no properties, and one byte left means that reason code with no properties.
    /// </summary>
    /// <param name="context">Where the property list stands.</param>
    /// <returns>The reason code and properties.</returns>
    public (Mqtt5ReasonCode ReasonCode, IReadOnlyList<Mqtt5Property> Properties) ReadOptionalReasonCodeAndProperties(
        Mqtt5PropertyContext context)
    {
        var reasonCode = Remaining > 0 ? (Mqtt5ReasonCode)ReadByte() : Mqtt5ReasonCode.Success;
        var properties = Remaining > 0 ? ReadProperties(context) : [];
        return (reasonCode, properties);
    }

    /// <summary>
    /// The property context of a packet type: bit <c>n</c> for type <c>n</c>.
    /// </summary>
    /// <param name="type">The packet type.</param>
    /// <returns>Its context.</returns>
    public static Mqtt5PropertyContext ContextOf(Mqtt5PacketType type) => (Mqtt5PropertyContext)(1 << (int)type);

    /// <summary>
    /// Refuses the packet as malformed when any byte is left unread.
    /// </summary>
    /// <param name="statement">The statement a trailing byte breaks.</param>
    public void RequireEnd(string statement)
    {
        if (Remaining > 0)
        {
            throw Fault(Mqtt5PacketFault.Malformed(statement));
        }
    }

    /// <summary>
    /// Wraps <paramref name="fault"/> for throwing.
    /// </summary>
    /// <param name="fault">What is wrong.</param>
    /// <returns>The exception to throw.</returns>
    public static Mqtt5PacketFaultException Fault(Mqtt5PacketFault fault) => new(fault);

    private Mqtt5Property ReadPropertyValue(Mqtt5PropertyDefinition definition) => definition.Type switch
    {
        Mqtt5PropertyType.Byte => Mqtt5Property.FromInteger(definition.Identifier, ReadByte()),
        Mqtt5PropertyType.TwoByteInteger => Mqtt5Property.FromInteger(definition.Identifier, ReadTwoByteInteger()),
        Mqtt5PropertyType.FourByteInteger => Mqtt5Property.FromInteger(definition.Identifier, ReadFourByteInteger()),
        Mqtt5PropertyType.VariableByteInteger => Mqtt5Property.FromInteger(definition.Identifier, (uint)ReadVariableByteInteger()),
        Mqtt5PropertyType.Utf8String => Mqtt5Property.FromText(definition.Identifier, ReadUtf8String()),
        Mqtt5PropertyType.BinaryData => Mqtt5Property.FromBinary(definition.Identifier, ReadBinaryData()),
        _ => Mqtt5Property.FromUserProperty(ReadUtf8String(), ReadUtf8String()),
    };

    // A length that runs past the packet (or past its property list) is a Malformed Packet (4.13.1).
    private ReadOnlyMemory<byte> Take(int count)
    {
        if (count > Remaining)
        {
            throw Fault(Mqtt5PacketFault.Malformed("4.13.1"));
        }

        var taken = body.Slice(position, count);
        position += count;
        return taken;
    }
}
