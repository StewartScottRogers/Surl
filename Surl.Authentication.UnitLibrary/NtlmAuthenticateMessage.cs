using System.Buffers.Binary;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// The parts of an <c>AUTHENTICATE_MESSAGE</c> ([MS-NLMP] section 2.2.1.3), NTLM's last leg,
/// that a server checks: the user and domain names, read as UTF-16LE when the message's own
/// <c>NegotiateFlags</c> say Unicode and one byte per character otherwise, and the
/// <c>NtChallengeResponse</c>. The LM response, workstation, session key and MIC are not read.
/// </summary>
/// <param name="UserName">The <c>UserName</c> as sent.</param>
/// <param name="DomainName">The <c>DomainName</c> as sent, often empty.</param>
/// <param name="NtChallengeResponse">The <c>NtChallengeResponse</c> bytes.</param>
internal sealed record NtlmAuthenticateMessage(string UserName, string DomainName, byte[] NtChallengeResponse)
{
    // Signature, MessageType, the six payload fields and NegotiateFlags.
    private const int MinimumLength = 64;

    private const int NtChallengeResponseField = 20;

    private const int DomainNameField = 28;

    private const int UserNameField = 36;

    private const int NegotiateFlagsOffset = 60;

    /// <summary>
    /// Reads <paramref name="message"/>, when it is a whole <c>AUTHENTICATE_MESSAGE</c>.
    /// </summary>
    /// <param name="message">The decoded message.</param>
    /// <returns>Its parts, or <see langword="null"/> when it is another message, too short, or
    /// a field points outside it.</returns>
    public static NtlmAuthenticateMessage? TryRead(ReadOnlySpan<byte> message)
    {
        if (message.Length < MinimumLength || NtlmMessage.ReadMessageType(message) != NtlmMessage.AuthenticateType)
        {
            return null;
        }

        var response = NtlmMessage.ReadPayload(message, NtChallengeResponseField);
        var domain = ReadString(message, DomainNameField);
        var user = ReadString(message, UserNameField);

        return response is null || domain is null || user is null
            ? null
            : new NtlmAuthenticateMessage(user, domain, response);
    }

    private static string? ReadString(ReadOnlySpan<byte> message, int fieldOffset)
    {
        var flags = (NtlmNegotiateFlags)BinaryPrimitives.ReadUInt32LittleEndian(message[NegotiateFlagsOffset..]);
        var encoding = flags.HasFlag(NtlmNegotiateFlags.Unicode) ? Encoding.Unicode : Encoding.Latin1;
        var payload = NtlmMessage.ReadPayload(message, fieldOffset);

        return payload is null ? null : encoding.GetString(payload);
    }
}
