using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// Verifies one method's <c>Authorization</c> credentials on one HTTP connection, from
/// <see cref="IHttpAuthenticationMethod.StartConnection"/>.
/// </summary>
public interface IHttpCredentialVerifier
{
    /// <summary>
    /// Checks the credentials that followed the scheme in an <c>Authorization</c> field.
    /// Every comparison of a secret is fixed-time (ADR-0032, section 8); any refusal delay is
    /// the policy's, not the verifier's.
    /// </summary>
    /// <param name="credentials">What followed the scheme and its spaces, as received.</param>
    /// <param name="request">The request the field arrived on.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Accepted, refused, or a handshake's next step.</returns>
    ValueTask<HttpCredentialCheck> VerifyAsync(
        string credentials, HttpAuthenticationRequest request, CancellationToken cancellationToken);
}
