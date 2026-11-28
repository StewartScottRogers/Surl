namespace Surl.Protocol.Ldap;

/// <summary>
/// The result of reading one <c>LDAPMessage</c>: its whole BER encoding, or the reason there is none.
/// </summary>
/// <param name="Outcome">How the read ended.</param>
/// <param name="Message">
/// The message's tag, length and value; <see langword="null"/> unless <paramref name="Outcome"/> is
/// <see cref="LdapFrameReadOutcome.FrameRead"/>.
/// </param>
/// <param name="AnnouncedBytes">
/// When <paramref name="Outcome"/> is <see cref="LdapFrameReadOutcome.MessageTooLarge"/>, the bytes
/// announced: for a message, the whole of it, tag and length included; for a security-layer buffer,
/// the protected bytes alone, without the 4 length bytes. 0 otherwise.
/// </param>
internal sealed record LdapFrameReadResult(LdapFrameReadOutcome Outcome, byte[]? Message, long AnnouncedBytes = 0)
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

    /// <summary>
    /// A result for a message or security-layer buffer whose length announced more than is allowed;
    /// none of the message's value, or of the buffer's protected bytes, was read.
    /// </summary>
    /// <param name="announcedBytes">
    /// For a message, the bytes it announced, tag and length included; for a security-layer buffer,
    /// the protected length it announced, without the 4 length bytes.
    /// </param>
    /// <returns>A <see cref="LdapFrameReadOutcome.MessageTooLarge"/> result.</returns>
    public static LdapFrameReadResult TooLarge(long announcedBytes) => new(LdapFrameReadOutcome.MessageTooLarge, null, announcedBytes);
}
