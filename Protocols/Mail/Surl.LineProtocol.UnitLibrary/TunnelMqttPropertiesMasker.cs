using System.Buffers;

namespace Surl.LineProtocol;

/// <summary>
/// Walks an MQTT 5 Property Length and its properties (5.0 section 2.2.2) and writes them with every
/// Authentication Data value masked (ADR-0182, amendment 28).
/// </summary>
internal static class TunnelMqttPropertiesMasker
{
    private const int AuthenticationData = 0x16;

    // The Variable Byte Integer (Subscription Identifier) and the UTF-8 String Pair (User Property).
    private const int SubscriptionIdentifier = 0x0B;
    private const int UserProperty = 0x26;

    // The identifiers of table 2-4 by their value's type.
    private static ReadOnlySpan<int> ByteProperties => [0x01, 0x17, 0x19, 0x24, 0x25, 0x28, 0x29, 0x2A];

    private static ReadOnlySpan<int> TwoByteIntegerProperties => [0x13, 0x21, 0x22, 0x23];

    private static ReadOnlySpan<int> FourByteIntegerProperties => [0x02, 0x11, 0x18, 0x27];

    // UTF-8 Strings and Binary Data alike are two length bytes and that many bytes.
    private static ReadOnlySpan<int> FieldProperties =>
        [0x03, 0x08, 0x09, 0x12, 0x15, AuthenticationData, 0x1A, 0x1C, 0x1F];

    /// <summary>
    /// Writes the Property Length and properties at <paramref name="cursor"/>: as they arrived when
    /// none is Authentication Data, else with each such value as a length of 4 and <c>****</c> and
    /// the Property Length counting what is written.
    /// </summary>
    /// <param name="body">The packet after its fixed header, whole or cut short.</param>
    /// <param name="cursor">Where the Property Length starts; moved past the properties.</param>
    /// <param name="output">Where they are appended; nothing is when they cannot be walked.</param>
    /// <returns>
    /// False when the Property Length or a property is cut short, or an identifier is not one of
    /// table 2-4's, so no Authentication Data can be ruled out.
    /// </returns>
    public static bool TryMask(ReadOnlySpan<byte> body, ref int cursor, ArrayBufferWriter<byte> output)
    {
        var start = cursor;
        if (!TunnelMqttFields.TryReadVariableByteInteger(body, ref cursor, out var length)
            || !TunnelMqttFields.TrySkip(body, ref cursor, length))
        {
            return false;
        }

        var list = body[(cursor - length)..cursor];
        var masked = new ArrayBufferWriter<byte>();
        var position = 0;
        while (position < list.Length)
        {
            if (!TryMaskProperty(list, ref position, masked))
            {
                return false;
            }
        }

        if (list.SequenceEqual(masked.WrittenSpan))
        {
            output.Write(body[start..cursor]);
        }
        else
        {
            TunnelMqttFields.WriteVariableByteInteger(masked.WrittenCount, output);
            output.Write(masked.WrittenSpan);
        }

        return true;
    }

    private static bool TryMaskProperty(ReadOnlySpan<byte> list, ref int position, ArrayBufferWriter<byte> output)
    {
        var start = position;
        if (!TunnelMqttFields.TryReadVariableByteInteger(list, ref position, out var identifier)
            || !TrySkipValue(list, ref position, identifier))
        {
            return false;
        }

        if (identifier == AuthenticationData)
        {
            output.Write([(byte)AuthenticationData]);
            output.Write(TunnelMqttFields.MaskedField);
        }
        else
        {
            output.Write(list[start..position]);
        }

        return true;
    }

    private static bool TrySkipValue(ReadOnlySpan<byte> list, ref int position, int identifier)
    {
        if (ByteProperties.Contains(identifier))
        {
            return TunnelMqttFields.TrySkip(list, ref position, sizeof(byte));
        }

        if (TwoByteIntegerProperties.Contains(identifier))
        {
            return TunnelMqttFields.TrySkip(list, ref position, sizeof(ushort));
        }

        if (FourByteIntegerProperties.Contains(identifier))
        {
            return TunnelMqttFields.TrySkip(list, ref position, sizeof(uint));
        }

        if (identifier == SubscriptionIdentifier)
        {
            return TunnelMqttFields.TryReadVariableByteInteger(list, ref position, out _);
        }

        if (identifier == UserProperty)
        {
            return TunnelMqttFields.TrySkipField(list, ref position) && TunnelMqttFields.TrySkipField(list, ref position);
        }

        return FieldProperties.Contains(identifier) && TunnelMqttFields.TrySkipField(list, ref position);
    }
}
