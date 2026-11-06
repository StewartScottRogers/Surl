using Surl.Http3;
using Surl.Protocol.Abstractions;
using Surl.Qpack;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// Judges a WebTransport <c>CONNECT</c>'s login through the connection's
/// <see cref="IHttpAuthenticationSession"/> (ADR-0117 decision 7; ADR-0032 section 6): the one
/// account set, the session's own refusal delay, and <c>--allow-anonymous</c> as the policy applies it.
/// </summary>
/// <remarks>
/// <c>Proceed</c> lets the request on to the path check; <c>Challenge</c> is <c>401</c> with the
/// verdict's <c>www-authenticate</c> values and <c>Forbidden</c> is <c>403</c>, neither naming the
/// account or the cause. NTLM and Negotiate are bound to a connection, and WebTransport has no
/// HTTP/1.1 to fall back to: credentials naming either are never judged, and are answered <c>401</c>
/// with the challenges the request earns without them; no answer offers either scheme.
/// </remarks>
/// <param name="authentication">The connection's authentication session.</param>
internal sealed class WebTransportConnectAuthentication(IHttpAuthenticationSession authentication)
{
    private const string AuthorizationField = "authorization";

    /// <summary>Judges <paramref name="head"/>'s login.</summary>
    /// <param name="head">The extended <c>CONNECT</c>'s head.</param>
    /// <param name="cancellationToken">Cancelled when the connection ends.</param>
    /// <returns>The <c>401</c> or <c>403</c> answer's fields, or <see langword="null"/> when the request proceeds.</returns>
    public async Task<QpackFieldLine[]?> RefuseAsync(Http3RequestHead head, CancellationToken cancellationToken)
    {
        var isConnectionBound = head.Fields.Any(field => field.Key == AuthorizationField && IsConnectionBoundScheme(field.Value));
        IReadOnlyList<KeyValuePair<string, string>> fields = isConnectionBound
            ? [.. head.Fields.Where(field => field.Key != AuthorizationField)]
            : head.Fields;
        var verdict = await authentication.JudgeAsync(new HttpAuthenticationRequest("CONNECT", head.Target, IsWrite: false, fields), cancellationToken);
        return (isConnectionBound, verdict.Outcome) switch
        {
            (false, HttpAuthenticationOutcome.Proceed) => null,
            (false, HttpAuthenticationOutcome.Forbidden) => [new(":status", "403")],
            _ => [new(":status", "401"), .. verdict.WwwAuthenticateValues.Where(value => !IsConnectionBoundScheme(value)).Select(value => new QpackFieldLine("www-authenticate", value))],
        };
    }

    // Whether a credentials or challenge value's auth-scheme (RFC 9110 section 11.1) is NTLM or Negotiate.
    private static bool IsConnectionBoundScheme(string value)
    {
        var scheme = value.Split(' ', 2)[0];
        return scheme.Equals("NTLM", StringComparison.OrdinalIgnoreCase) || scheme.Equals("Negotiate", StringComparison.OrdinalIgnoreCase);
    }
}
