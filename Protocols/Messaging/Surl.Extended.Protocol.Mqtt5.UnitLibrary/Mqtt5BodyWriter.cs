using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Writes the data representations of MQTT 5.0 section 1.5 and property lists (section 2.2.2)
/// into a packet's body, front to back. A value its representation cannot hold throws -
/// <see cref="ArgumentOutOfRangeException"/>, <see cref="OverflowException"/> for a Byte or Two
/// Byte Integer property, <see cref="EncoderFallbackException"/> for an unpaired surrogate: the
/// broker never builds such a packet, so it is a defect, not a wire condition.
/// </summary>
internal sealed class Mqtt5BodyWriter
{
    /// <summary>
    /// The largest Variable Byte Integer (section 1.5.5).
    /// </summary>
    public const int MaxVariableByteInteger = 268_435_455;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ArrayBufferWriter<byte> written = new();

    /// <summary>
    /// How many bytes have been written.
    /// </summary>
    public int Length => written.WrittenCount;

    /// <summary>
    /// The bytes written so far.
    /// </summary>
    public ReadOnlySpan<byte> WrittenSpan => written.WrittenSpan;

    /// <summary>
    /// Writes one byte (section 1.5.1).
    /// </summary>
    /// <param name="value">The byte.</param>
    public void WriteByte(byte value) => WriteBytes([value]);

    /// <summary>
    /// Writes a big-endian 16-bit integer (section 1.5.2).
    /// </summary>
    /// <param name="value">The integer.</param>
    public void WriteTwoByteInteger(ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(written.GetSpan(2), value);
        written.Advance(2);
    }

    /// <summary>
    /// Writes a big-endian 32-bit integer (section 1.5.3).
    /// </summary>
    /// <param name="value">The integer.</param>
    public void WriteFourByteInteger(uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(written.GetSpan(4), value);
        written.Advance(4);
    }

    /// <summary>
    /// Writes a Variable Byte Integer in the fewest bytes that hold it (section 1.5.5,
    /// <c>[MQTT-1.5.5-1]</c>).
    /// </summary>
    /// <param name="value">0 to 268435455.</param>
    public void WriteVariableByteInteger(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxVariableByteInteger);
        do
        {
            var encodedByte = (byte)(value % 128);
            value /= 128;
            WriteByte(value > 0 ? (byte)(encodedByte | 0x80) : encodedByte);
        }
        while (value > 0);
    }

    /// <summary>
    /// Writes a UTF-8 Encoded String (section 1.5.4).
    /// </summary>
    /// <param name="value">The string; at most 65535 bytes once encoded, with no unpaired surrogate.</param>
    public void WriteUtf8String(string value) => WriteBinaryData(StrictUtf8.GetBytes(value));

    /// <summary>
    /// Writes Binary Data (section 1.5.6): a two-byte length, then the bytes.
    /// </summary>
    /// <param name="value">At most 65535 bytes.</param>
    public void WriteBinaryData(ReadOnlySpan<byte> value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, ushort.MaxValue, nameof(value));
        WriteTwoByteInteger((ushort)value.Length);
        WriteBytes(value);
    }

    /// <summary>
    /// Writes bytes as they are, as a <c>PUBLISH</c> payload is (section 3.3.3).
    /// </summary>
    /// <param name="value">The bytes.</param>
    public void WriteBytes(ReadOnlySpan<byte> value) => written.Write(value);

    /// <summary>
    /// Writes a property list (section 2.2.2): its Variable Byte Integer length, then each
    /// property in order.
    /// </summary>
    /// <param name="properties">The properties.</param>
    public void WriteProperties(IReadOnlyList<Mqtt5Property> properties)
    {
        var list = new Mqtt5BodyWriter();
        foreach (var property in properties)
        {
            list.WriteVariableByteInteger((int)property.Identifier);
            list.WritePropertyValue(property);
        }

        WriteVariableByteInteger(list.Length);
        WriteBytes(list.WrittenSpan);
    }

    /// <summary>
    /// Writes the tail of a packet whose Reason Code and Property Length may be left off
    /// (sections 3.4.2.1, 3.14.2.1, 3.15.2.1 and their peers), in the fewest bytes: nothing
    /// for Success with no properties, the reason code alone with no properties.
    /// </summary>
    /// <param name="reasonCode">The reason code.</param>
    /// <param name="properties">The properties.</param>
    public void WriteOptionalReasonCodeAndProperties(Mqtt5ReasonCode reasonCode, IReadOnlyList<Mqtt5Property> properties)
    {
        if (reasonCode == Mqtt5ReasonCode.Success && properties.Count == 0)
        {
            return;
        }

        WriteByte((byte)reasonCode);
        if (properties.Count > 0)
        {
            WriteProperties(properties);
        }
    }

    private void WritePropertyValue(Mqtt5Property property)
    {
        switch (property.Type)
        {
            case Mqtt5PropertyType.Byte:
                WriteByte(checked((byte)property.Integer));
                break;
            case Mqtt5PropertyType.TwoByteInteger:
                WriteTwoByteInteger(checked((ushort)property.Integer));
                break;
            case Mqtt5PropertyType.FourByteInteger:
                WriteFourByteInteger(property.Integer);
                break;
            case Mqtt5PropertyType.VariableByteInteger:
                WriteVariableByteInteger(property.Integer);
                break;
            case Mqtt5PropertyType.Utf8String:
                WriteUtf8String(property.Text);
                break;
            case Mqtt5PropertyType.BinaryData:
                WriteBinaryData(property.Binary);
                break;
            default:
                WriteUtf8String(property.Text);
                WriteUtf8String(property.PairValue);
                break;
        }
    }
}
