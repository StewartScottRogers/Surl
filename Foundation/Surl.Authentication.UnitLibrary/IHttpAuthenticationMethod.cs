namespace Surl.Authentication;

/// <summary>
/// One HTTP authentication method's challenge and verifier (ADR-0032, sections 3 and 4). Each
/// method is its own task (BL-111 Basic and Bearer, BL-113 Digest, BL-120 NTLM, BL-121
/// Negotiate, BL-122 Signature Version 4); <see cref="AuthenticationPolicy"/> decides everything
/// they share - who needs a login, the plain-text refusal, the order and the refusal delay.
/// </summary>
public interface IHttpAuthenticationMethod
{
    /// <summary>
    /// The method this verifies.
    /// </summary>
    AuthenticationMethod Method { get; }

    /// <summary>
    /// The <c>WWW-Authenticate</c> values offering this method on a <c>401</c>, in order: none
    /// for a method with no challenge (Signature Version 4), three for Digest.
    /// </summary>
    /// <returns>The values, each written as one field.</returns>
    IReadOnlyList<string> CreateChallenges();

    /// <summary>
    /// Starts this method's state for one HTTP connection: NTLM's and Negotiate's handshakes
    /// live in it and die with the connection.
    /// </summary>
    /// <returns>The verifier for every <c>Authorization</c> of this method on the connection.</returns>
    IHttpCredentialVerifier StartConnection();
}
