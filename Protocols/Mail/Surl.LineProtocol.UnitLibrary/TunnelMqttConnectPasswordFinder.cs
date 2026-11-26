using System.Buffers.Binary;

namespace Surl.LineProtocol;

/// <summary>
/// Walks the body of an MQTT <c>CONNECT</c> (3.1.1 section 3.1; 5.0 section 3.1, which adds
/// properties) to the bytes a log must not hold. This library keeps its own walk: it may not
/// reference an MQTT server (ADR-0002 decision 3).
/// </summary>
internal static class TunnelMqttConnectPasswordFinder
{
    private const byte UserNameFlag = 0x80;
    private const byte PasswordFlag = 0x40;
    private const byte WillFlag = 0x04;
    private const int FirstPropertiesProtocolLevel = 5;
    private const int MaxVariableByteIntegerBytes = 4;

    // The protocol level, the connect flags and the two bytes of the keep alive.
    private const int BytesAfterProtocolName = 4;

    /// <summary>
    /// Finds where the secret of a <c>CONNECT</c> body starts. It runs to the end of the body,
    /// because the password is the payload's last field.
    /// </summary>
    /// <param name="body">The packet after its fixed header, whole or cut short.</param>
    /// <param name="start">Where the secret starts.</param>
    /// <param name="isPasswordField">
    /// True when <paramref name="start"/> is the password's two length bytes and the password is
    /// exactly the rest of the body; false when the other fields do not lead there, and
    /// <paramref name="start"/> is the first byte after the keep alive.
    /// </param>
    /// <returns>False when the body has no connect flags or they announce no password.</returns>
    public static bool TryFindSecret(ReadOnlySpan<byte> body, out int start, out bool isPasswordField)
    {
        isPasswordField = false;
        start = 0;
        if (!TrySkipField(body, ref start) || body.Length < start + BytesAfterProtocolName)
        {
            return false;
        }

        var level = body[start];
        var flags = body[start + 1];
        start += BytesAfterProtocolName;
        if ((flags & PasswordFlag) == 0)
        {
            return false;
        }

        var cursor = start;
        if (TrySkipFieldsBeforePassword(body, ref cursor, level, flags) && IsLastField(body[cursor..]))
        {
            start = cursor;
            isPasswordField = true;
        }

        return true;
    }

    /// <summary>Reads a variable byte integer (3.1.1 section 2.2.3) at <paramref name="cursor"/>.</summary>
    /// <param name="bytes">The bytes holding it.</param>
    /// <param name="cursor">Where it starts; moved past it as far as it was read.</param>
    /// <param name="value">Its value.</param>
    /// <returns>False when it is cut short or runs past four bytes.</returns>
    public static bool TryReadVariableByteInteger(ReadOnlySpan<byte> bytes, ref int cursor, out int value)
    {
        value = 0;
        for (var index = 0; index < MaxVariableByteIntegerBytes && cursor < bytes.Length; index++)
        {
            var next = bytes[cursor++];
            value |= (next & 0x7F) << (7 * index);
            if ((next & 0x80) == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLastField(ReadOnlySpan<byte> rest)
    {
        var end = 0;
        return TrySkipField(rest, ref end) && end == rest.Length;
    }

    private static bool TrySkipFieldsBeforePassword(ReadOnlySpan<byte> body, ref int cursor, byte level, byte flags)
    {
        var hasProperties = level >= FirstPropertiesProtocolLevel;
        return (!hasProperties || TrySkipProperties(body, ref cursor))
            && TrySkipField(body, ref cursor)
            && ((flags & WillFlag) == 0 || TrySkipWill(body, ref cursor, hasProperties))
            && ((flags & UserNameFlag) == 0 || TrySkipField(body, ref cursor));
    }

    private static bool TrySkipWill(ReadOnlySpan<byte> body, ref int cursor, bool hasProperties) =>
        (!hasProperties || TrySkipProperties(body, ref cursor))
        && TrySkipField(body, ref cursor)
        && TrySkipField(body, ref cursor);

    // A field is two length bytes and that many bytes.
    private static bool TrySkipField(ReadOnlySpan<byte> body, ref int cursor) =>
        body.Length >= cursor + sizeof(ushort)
        && TrySkip(body, ref cursor, sizeof(ushort) + BinaryPrimitives.ReadUInt16BigEndian(body[cursor..]));

    private static bool TrySkipProperties(ReadOnlySpan<byte> body, ref int cursor) =>
        TryReadVariableByteInteger(body, ref cursor, out var length) && TrySkip(body, ref cursor, length);

    private static bool TrySkip(ReadOnlySpan<byte> body, ref int cursor, int count)
    {
        if (body.Length - cursor < count)
        {
            return false;
        }

        cursor += count;
        return true;
    }
}
