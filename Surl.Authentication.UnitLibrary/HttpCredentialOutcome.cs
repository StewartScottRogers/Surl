namespace Surl.Authentication;

/// <summary>
/// What an <see cref="IHttpCredentialVerifier"/> made of an <c>Authorization</c> field
/// (ADR-0032, section 4, step 3).
/// </summary>
public enum HttpCredentialOutcome
{
    /// <summary>
    /// The credentials match an account: the request is served.
    /// </summary>
    Accepted,

    /// <summary>
    /// A handshake needs another round (NTLM's type 2 message, a Negotiate token): a <c>401</c>
    /// carrying that step's one value, not delayed.
    /// </summary>
    Continue,

    /// <summary>
    /// The credentials match no account, or are malformed: a <c>401</c> with every challenge,
    /// after the refusal delay.
    /// </summary>
    Refused,
}
