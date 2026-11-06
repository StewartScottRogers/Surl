using System.Buffers.Binary;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// The parts of an <c>AUTHENTICATE_MESSAGE</c> ([MS-NLMP] section 2.2.1.3), NTLM's last leg,
/// that a server checks or derives the session keys from: the user and domain names, read as
/// UTF-16LE when the message's own <c>NegotiateFlags</c> say Unicode and one byte per character
/// otherwise, the <c>NtChallengeResponse</c>, the <c>EncryptedRandomSessionKey</c> and the
/// <c>NegotiateFlags</c>. The LM response, workstation and MIC are not read.
/// </summary>
/// <param name="UserName">The <c>UserName</c> as sent.</param>
/// <param name="DomainName">The <c>DomainName</c> as sent, often empty.</param>
/// <param name="NtChallengeResponse">The <c>NtChallengeResponse</c> bytes.</param>
/// <param name="EncryptedRandomSessionKey">The <c>EncryptedRandomSessionKey</c> bytes, empty when none was sent.</param>
/// <param name="NegotiateFlags">The flags the client settled on.</param>
internal sealed record NtlmAuthenticateMessage(
    string UserName,
    string DomainName,
    byte[] NtChallengeResponse,
    byte[] EncryptedRandomSessionKey,
    NtlmNegotiateFlags NegotiateFlags)
{
    // Signature, MessageType, the six payload fields and NegotiateFlags.
    private const int MinimumLength = 64;

    private const int NtChallengeResponseField = 20;

    private const int DomainNameField = 28;

    private const int UserNameField = 36;

    private const int EncryptedRandomSessionKeyField = 52;

    private const int NegotiateFlagsOffset = 60;

    /// <summary>
    /// Reads <paramref name="message"/>, when it is a whole <c>AUTHENTICATE_MESSAGE</c>.
    /// </summary>
    /// <param name="message">The decoded message.</param>
    /// <returns>Its parts, or <see langword="null"/> when it is another message, too short, or
    /// a field points outside it.</returns>
    public static NtlmAuthenticateMessage? TryRead(ReadOnlySpan<byte> message)
    {
        if (!IsAuthenticateMessage(message))
        {
            return null;
        }

        var flags = (NtlmNegotiateFlags)BinaryPrimitives.ReadUInt32LittleEndian(message[NegotiateFlagsOffset..]);
        var response = NtlmMessage.ReadPayload(message, NtChallengeResponseField);
        var domain = ReadString(message, DomainNameField, flags);
        var user = ReadString(message, UserNameField, flags);
        var sessionKey = NtlmMessage.ReadPayload(message, EncryptedRandomSessionKeyField);

        return response is null || domain is null || user is null || sessionKey is null
            ? null
            : new NtlmAuthenticateMessage(user, domain, response, sessionKey, flags);
    }

    private static bool IsAuthenticateMessage(ReadOnlySpan<byte> message) =>
        message.Length >= MinimumLength && NtlmMessage.ReadMessageType(message) == NtlmMessage.AuthenticateType;

    private static string? ReadString(ReadOnlySpan<byte> message, int fieldOffset, NtlmNegotiateFlags flags)
    {
        var encoding = flags.HasFlag(NtlmNegotiateFlags.Unicode) ? Encoding.Unicode : Encoding.Latin1;
        var payload = NtlmMessage.ReadPayload(message, fieldOffset);

        return payload is null ? null : encoding.GetString(payload);
    }
}
