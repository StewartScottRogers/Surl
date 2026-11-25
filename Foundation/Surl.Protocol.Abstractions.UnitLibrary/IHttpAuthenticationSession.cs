namespace Surl.Protocol.Abstractions;

/// <summary>
/// The authentication state of one HTTP connection, from
/// <see cref="IAuthenticationPolicy.StartHttpConnection(TlsSession?)"/> (ADR-0032, section 6). NTLM's and
/// Negotiate's handshakes live here and die with it; it holds no unmanaged resource, so it is
/// not disposable.
/// </summary>
public interface IHttpAuthenticationSession
{
    /// <summary>
    /// Judges one request on the connection: whether it is served, challenged with a
    /// <c>401</c> or refused with a <c>403</c> (ADR-0032, sections 4 and 6).
    /// </summary>
    /// <param name="request">The request's method, target and header fields.</param>
    /// <param name="cancellationToken">Cancels the judgement.</param>
    /// <returns>The outcome, the <c>WWW-Authenticate</c> values to send and the account logged in.</returns>
    ValueTask<HttpAuthenticationVerdict> JudgeAsync(
        HttpAuthenticationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The <c>WWW-Authenticate</c> values a <c>401</c> on this connection would offer now, for a
    /// server answering a <c>401</c> that did not come from <see cref="JudgeAsync"/>, since
    /// RFC 9110 section 11.6.1 requires one challenge at least. It judges no request, logs no one
    /// in and changes no login. The default offers none.
    /// </summary>
    /// <returns>The values in the order to send them; empty when the session offers no method on this connection.</returns>
    IReadOnlyList<string> CreateChallenges() => [];
}
