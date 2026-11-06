namespace Surl.Protocol.Ldap;

/// <summary>
/// The result of decoding one <c>LDAPMessage</c>: the message, or why it is malformed.
/// </summary>
/// <param name="Outcome">How the decode ended.</param>
/// <param name="MessageId">
/// The <c>messageID</c> whenever it could be read, so a malformed message can still be answered
/// by its ID; <see langword="null"/> when it could not.
/// </param>
/// <param name="Message">
/// The message; <see langword="null"/> unless <paramref name="Outcome"/> is <see cref="LdapDecodeOutcome.Decoded"/>.
/// </param>
internal sealed record LdapDecodeResult(LdapDecodeOutcome Outcome, int? MessageId, LdapMessage? Message)
{
    /// <summary>
    /// A result that carries a message.
    /// </summary>
    /// <param name="message">The message decoded.</param>
    /// <returns>A <see cref="LdapDecodeOutcome.Decoded"/> result.</returns>
    public static LdapDecodeResult Decoded(LdapMessage message) => new(LdapDecodeOutcome.Decoded, message.MessageId, message);

    /// <summary>
    /// A result for a malformed message.
    /// </summary>
    /// <param name="outcome">What is malformed.</param>
    /// <param name="messageId">The <c>messageID</c>, when it could be read.</param>
    /// <returns>A result with <see cref="Message"/> <see langword="null"/>.</returns>
    public static LdapDecodeResult Malformed(LdapDecodeOutcome outcome, int? messageId) => new(outcome, messageId, null);
}
