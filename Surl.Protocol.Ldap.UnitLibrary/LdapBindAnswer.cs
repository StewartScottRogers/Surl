using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// How a <c>BindRequest</c> is answered (ADR-0072 decisions 2 and 4): the <c>BindResponse</c> to
/// send, and what the connection is afterwards.
/// </summary>
/// <param name="Result">The response's result.</param>
/// <param name="IsBound">Whether the connection is bound once the response is sent.</param>
/// <param name="ServerSaslCredentials">The <c>serverSaslCreds</c>; <see langword="null"/> leaves the field out.</param>
/// <param name="SicilyMatchedDn">
/// The octets a Sicily answer carries as its <c>matchedDN</c> in place of
/// <see cref="LdapResult.MatchedDn"/>; <see langword="null"/> for every other answer.
/// </param>
/// <param name="SecurityLayer">The security layer that protects every message after the response; <see langword="null"/> for none.</param>
/// <param name="Mechanism">The mechanism that negotiated <paramref name="SecurityLayer"/>, for the log.</param>
internal sealed record LdapBindAnswer(
    LdapResult Result,
    bool IsBound,
    byte[]? ServerSaslCredentials = null,
    byte[]? SicilyMatchedDn = null,
    ISaslSecurityLayer? SecurityLayer = null,
    string? Mechanism = null)
{
    /// <summary>
    /// The answer that refuses a bind with <paramref name="result"/> and leaves the connection anonymous.
    /// </summary>
    /// <param name="result">The refusal.</param>
    /// <returns>The answer.</returns>
    public static LdapBindAnswer Refused(LdapResult result) => new(result, IsBound: false);

    /// <summary>
    /// The <c>BindResponse</c>'s encoding.
    /// </summary>
    /// <param name="messageId">The <c>messageID</c> of the bind answered.</param>
    /// <returns>The message's encoding.</returns>
    public byte[] Encode(int messageId) => SicilyMatchedDn is { } matchedDn
        ? LdapMessageEncoder.EncodeSicilyBindResponse(messageId, matchedDn)
        : LdapMessageEncoder.EncodeBindResponse(messageId, Result, ServerSaslCredentials);
}
