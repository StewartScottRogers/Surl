namespace Surl.Protocol.Ldap;

/// <summary>
/// The result of reading one <c>LDAPMessage</c>: its whole BER encoding, or the reason there is none.
/// </summary>
/// <param name="Outcome">How the read ended.</param>
/// <param name="Message">
/// The message's tag, length and value; <see langword="null"/> unless <paramref name="Outcome"/> is
/// <see cref="LdapFrameReadOutcome.FrameRead"/>.
/// </param>
internal sealed record LdapFrameReadResult(LdapFrameReadOutcome Outcome, byte[]? Message)
{
    /// <summary>
    /// A result that carries a message.
    /// </summary>
    /// <param name="message">The message's whole encoding.</param>
    /// <returns>A <see cref="LdapFrameReadOutcome.FrameRead"/> result.</returns>
    public static LdapFrameReadResult Read(byte[] message) => new(LdapFrameReadOutcome.FrameRead, message);

    /// <summary>
    /// A result that carries no message.
    /// </summary>
    /// <param name="outcome">Why there is no message.</param>
    /// <returns>A result with <see cref="Message"/> <see langword="null"/>.</returns>
    public static LdapFrameReadResult NoFrame(LdapFrameReadOutcome outcome) => new(outcome, null);
}
