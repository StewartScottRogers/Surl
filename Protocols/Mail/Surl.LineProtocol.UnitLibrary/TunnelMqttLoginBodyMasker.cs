using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// Walks the body of an MQTT <c>CONNECT</c> (3.1.1 section 3.1; 5.0 section 3.1, which adds
/// properties) or MQTT 5 <c>AUTH</c> (section 3.15) and gives it as a log may hold it: with the
/// password and every Authentication Data value masked (ADR-0182, amendments 10 and 28). Where the
/// walk past a <c>CONNECT</c>'s keep alive or an <c>AUTH</c>'s reason code fails, so that a secret
/// cannot be told apart, everything it was walking is masked. A <c>CONNECT</c> whose keep alive
/// cannot be found, its protocol name length running past it included, is given as it arrived.
/// </summary>
internal static class TunnelMqttLoginBodyMasker
{
    private const byte UserNameFlag = 0x80;
    private const byte PasswordFlag = 0x40;
    private const byte WillFlag = 0x04;
    private const int FirstPropertiesProtocolLevel = 5;

    // The protocol level, the connect flags and the two bytes of the keep alive.
    private const int BytesAfterProtocolName = 4;

    /// <summary>
    /// Masks a <c>CONNECT</c> body. One that announces a password, or is level 5 or higher, whose
    /// fields do not lead exactly to its end has everything after its keep alive written as
    /// <c>****</c>.
    /// </summary>
    /// <param name="body">The packet after its fixed header, whole or cut short.</param>
    /// <returns>
    /// The body to log: as given when it holds no secret, or when it ends before its keep alive does.
    /// </returns>
    public static byte[] MaskConnect(ReadOnlySpan<byte> body)
    {
        var cursor = 0;
        if (!TunnelMqttFields.TrySkipField(body, ref cursor) || body.Length < cursor + BytesAfterProtocolName)
        {
            return body.ToArray();
        }

        var hasProperties = body[cursor] >= FirstPropertiesProtocolLevel;
        var flags = body[cursor + 1];
        cursor += BytesAfterProtocolName;
        if (!hasProperties && (flags & PasswordFlag) == 0)
        {
            return body.ToArray();
        }

        var payload = new ArrayBufferWriter<byte>();
        return TryMaskAfterKeepAlive(body, cursor, hasProperties, flags, payload)
            ? [.. body[..cursor], .. payload.WrittenSpan]
            : [.. body[..cursor], .. LineBytesRedactor.SecretMask];
    }

    /// <summary>
    /// Masks an <c>AUTH</c> body: a reason code, then nothing or the properties. One whose
    /// properties do not end exactly where it does is written as <c>****</c>.
    /// </summary>
    /// <param name="body">The packet after its fixed header, whole or cut short.</param>
    /// <returns>The body to log: as given when it holds no Authentication Data.</returns>
    public static byte[] MaskAuth(ReadOnlySpan<byte> body)
    {
        if (body.Length <= sizeof(byte))
        {
            return body.ToArray();
        }

        var cursor = sizeof(byte);
        var masked = new ArrayBufferWriter<byte>();
        masked.Write(body[..cursor]);
        return TunnelMqttPropertiesMasker.TryMask(body, ref cursor, masked) && cursor == body.Length
            ? masked.WrittenSpan.ToArray()
            : LineBytesRedactor.SecretMask.ToArray();
    }

    // The CONNECT properties of level 5, then the payload: client identifier, Will, user name, password.
    private static bool TryMaskAfterKeepAlive(
        ReadOnlySpan<byte> body,
        int cursor,
        bool hasProperties,
        byte flags,
        ArrayBufferWriter<byte> output) =>
        (!hasProperties || TunnelMqttPropertiesMasker.TryMask(body, ref cursor, output))
        && TryCopyField(body, ref cursor, output)
        && ((flags & WillFlag) == 0 || TryMaskWill(body, ref cursor, hasProperties, output))
        && ((flags & UserNameFlag) == 0 || TryCopyField(body, ref cursor, output))
        && TryMaskPasswordToTheEnd(body, cursor, flags, output);

    private static bool TryMaskWill(
        ReadOnlySpan<byte> body,
        ref int cursor,
        bool hasProperties,
        ArrayBufferWriter<byte> output) =>
        (!hasProperties || TunnelMqttPropertiesMasker.TryMask(body, ref cursor, output))
        && TryCopyField(body, ref cursor, output)
        && TryCopyField(body, ref cursor, output);

    private static bool TryCopyField(ReadOnlySpan<byte> body, ref int cursor, ArrayBufferWriter<byte> output)
    {
        var start = cursor;
        if (!TunnelMqttFields.TrySkipField(body, ref cursor))
        {
            return false;
        }

        output.Write(body[start..cursor]);
        return true;
    }

    // The password, when the flags announce one, is the payload's last field.
    private static bool TryMaskPasswordToTheEnd(
        ReadOnlySpan<byte> body,
        int cursor,
        byte flags,
        ArrayBufferWriter<byte> output)
    {
        if ((flags & PasswordFlag) != 0)
        {
            if (!TunnelMqttFields.TrySkipField(body, ref cursor))
            {
                return false;
            }

            output.Write(TunnelMqttFields.MaskedField);
        }

        return cursor == body.Length;
    }
}
