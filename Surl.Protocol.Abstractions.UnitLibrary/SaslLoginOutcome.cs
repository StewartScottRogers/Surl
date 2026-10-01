namespace Surl.Protocol.Abstractions;

/// <summary>
/// What a <see cref="SaslLoginStep"/> tells the server to do (ADR-0049, sections 6 and 7).
/// </summary>
public enum SaslLoginOutcome
{
    /// <summary>
    /// Send <see cref="SaslLoginStep.Challenge"/> as a continuation and read the next response.
    /// </summary>
    Challenge,

    /// <summary>
    /// The credentials were checked and match an account.
    /// </summary>
    Accepted,

    /// <summary>
    /// The login is accepted without its credentials being checked (<c>--allow-anonymous</c>,
    /// ADR-0038), so no <see cref="CheckedLogin"/> note is written for it.
    /// </summary>
    AcceptedUnchecked,

    /// <summary>
    /// The credentials were checked and refused, after ADR-0032 section 8's delay.
    /// </summary>
    RefusedCredentials,

    /// <summary>
    /// A plain-text mechanism was started without TLS and without <c>--allow-plaintext-auth</c>;
    /// no credentials were read.
    /// </summary>
    RefusedPlaintext,

    /// <summary>
    /// The mechanism is unknown, not accepted, or not offered on this connection.
    /// </summary>
    RefusedMechanism,
}
