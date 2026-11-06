using System.Buffers.Binary;

namespace Surl.Authentication;

/// <summary>
/// What every NTLM message shares ([MS-NLMP] section 2.2.1): the eight-byte signature
/// <c>NTLMSSP\0</c>, the 32-bit little-endian <c>MessageType</c> after it, and payload fields,
/// each a 16-bit length, a 16-bit maximum length and a 32-bit offset from the message's start.
/// </summary>
internal static class NtlmMessage
{
    /// <summary>
    /// <c>MessageType</c> of a <c>NEGOTIATE_MESSAGE</c>.
    /// </summary>
    public const uint NegotiateType = 1;

    /// <summary>
    /// <c>MessageType</c> of a <c>CHALLENGE_MESSAGE</c>.
    /// </summary>
    public const uint ChallengeType = 2;

    /// <summary>
    /// <c>MessageType</c> of an <c>AUTHENTICATE_MESSAGE</c>.
    /// </summary>
    public const uint AuthenticateType = 3;

    /// <summary>
    /// The length of the signature and <c>MessageType</c> together.
    /// </summary>
    public const int PrefixLength = 12;

    /// <summary>
    /// The signature every NTLM message starts with.
    /// </summary>
    public static ReadOnlySpan<byte> Signature => "NTLMSSP\0"u8;

    /// <summary>
    /// The <c>MessageType</c> of <paramref name="message"/>, when it starts with the signature.
    /// </summary>
    /// <param name="message">The decoded message.</param>
    /// <returns>The type, or <see langword="null"/> when the message is too short or not NTLM.</returns>
    public static uint? ReadMessageType(ReadOnlySpan<byte> message) =>
        message.Length >= PrefixLength && message.StartsWith(Signature)
            ? BinaryPrimitives.ReadUInt32LittleEndian(message[Signature.Length..])
            : null;

    /// <summary>
    /// The payload a field at <paramref name="fieldOffset"/> points at.
    /// </summary>
    /// <param name="message">The decoded message, at least <paramref name="fieldOffset"/> + 8 bytes long.</param>
    /// <param name="fieldOffset">Where the field's length, maximum length and offset start.</param>
    /// <returns>A copy of the payload, or <see langword="null"/> when it lies outside the message.</returns>
    public static byte[]? ReadPayload(ReadOnlySpan<byte> message, int fieldOffset)
    {
        var length = BinaryPrimitives.ReadUInt16LittleEndian(message[fieldOffset..]);
        var offset = (long)BinaryPrimitives.ReadUInt32LittleEndian(message[(fieldOffset + 4)..]);

        return offset + length <= message.Length ? message.Slice((int)offset, length).ToArray() : null;
    }

    /// <summary>
    /// Writes a field that points at <paramref name="length"/> payload bytes at <paramref name="offset"/>.
    /// </summary>
    /// <param name="destination">The eight bytes the field takes.</param>
    /// <param name="length">The payload's length, also written as its maximum length.</param>
    /// <param name="offset">The payload's offset from the message's start.</param>
    public static void WriteField(Span<byte> destination, int length, int offset)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)length);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], (ushort)length);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], (uint)offset);
    }
}
