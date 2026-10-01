namespace Surl.Protocol.Ldap;

/// <summary>
/// The diagnostic a Notice of Disconnection gives for a message the server could not read
/// (ADR-0072 decision 3): short ASCII naming the fault, never a peer's value.
/// </summary>
internal static class LdapDiagnostics
{
    /// <summary>
    /// The diagnostic for a message the frame reader refused.
    /// </summary>
    /// <param name="outcome">Why there was no message: <see cref="LdapFrameReadOutcome.NotASequence"/>,
    /// <see cref="LdapFrameReadOutcome.IndefiniteLength"/> or <see cref="LdapFrameReadOutcome.MalformedLength"/>.</param>
    /// <returns>The diagnostic.</returns>
    public static string Of(LdapFrameReadOutcome outcome) => outcome switch
    {
        LdapFrameReadOutcome.NotASequence => "not an LDAPMessage",
        LdapFrameReadOutcome.IndefiniteLength => "an indefinite length",
        _ => "a length of more than four octets",
    };

    /// <summary>
    /// The diagnostic for a message the decoder refused.
    /// </summary>
    /// <param name="outcome">Why the message did not decode; never <see cref="LdapDecodeOutcome.Decoded"/>.</param>
    /// <returns>The diagnostic.</returns>
    public static string Of(LdapDecodeOutcome outcome) => outcome switch
    {
        LdapDecodeOutcome.MalformedTagOrLength => "a malformed tag or length",
        LdapDecodeOutcome.IndefiniteLength => "an indefinite length",
        LdapDecodeOutcome.UnexpectedTag => "an unexpected tag",
        LdapDecodeOutcome.MissingElement => "a missing element",
        LdapDecodeOutcome.TrailingBytes => "trailing bytes",
        LdapDecodeOutcome.EnumerationOutOfRange => "an enumeration out of range",
        LdapDecodeOutcome.InvalidValue => "an invalid value",
        _ => "a filter nested too deep",
    };
}
