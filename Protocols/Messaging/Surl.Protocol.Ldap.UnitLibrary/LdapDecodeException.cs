namespace Surl.Protocol.Ldap;

/// <summary>
/// Thrown inside <see cref="LdapMessageDecoder"/> to abandon a malformed message; the decoder
/// catches it and returns its <see cref="Outcome"/>, so it never escapes the decoder.
/// </summary>
/// <param name="outcome">What is malformed.</param>
internal sealed class LdapDecodeException(LdapDecodeOutcome outcome) : Exception(outcome.ToString())
{
    /// <summary>
    /// What is malformed.
    /// </summary>
    public LdapDecodeOutcome Outcome { get; } = outcome;
}
