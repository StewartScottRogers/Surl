namespace Surl.Protocol.Abstractions;

/// <summary>
/// What the HTTP server does with a request, as an <see cref="HttpAuthenticationVerdict"/>
/// says (ADR-0032, section 6).
/// </summary>
public enum HttpAuthenticationOutcome
{
    /// <summary>
    /// Serve the request, writing any <see cref="HttpAuthenticationVerdict.WwwAuthenticateValues"/>
    /// (Negotiate's final token) on the response.
    /// </summary>
    Proceed,

    /// <summary>
    /// Answer <c>401 Unauthorized</c> with exactly the verdict's <c>WWW-Authenticate</c> values.
    /// </summary>
    Challenge,

    /// <summary>
    /// Answer <c>403 Forbidden</c>.
    /// </summary>
    Forbidden,
}
