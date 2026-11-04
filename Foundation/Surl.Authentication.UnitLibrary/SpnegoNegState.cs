namespace Surl.Authentication;

/// <summary>
/// A <c>negTokenResp</c>'s <c>negState</c> (RFC 4178 section 4.2.2), as its ENUMERATED values.
/// Surl sends only the two its NTLM handshake needs (ADR-0040).
/// </summary>
internal enum SpnegoNegState
{
    /// <summary>
    /// <c>accept-completed</c>: the login succeeded.
    /// </summary>
    AcceptCompleted = 0,

    /// <summary>
    /// <c>accept-incomplete</c>: another round is needed.
    /// </summary>
    AcceptIncomplete = 1,
}
