using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// An <see cref="IHttpAuthenticationMethod"/> that offers fixed challenges and answers every
/// credential with a fixed check, recording what it was asked.
/// </summary>
internal sealed class ScriptedHttpAuthenticationMethod(
    AuthenticationMethod method,
    IReadOnlyList<string> challenges,
    HttpCredentialCheck check) : IHttpAuthenticationMethod
{
    public AuthenticationMethod Method => method;

    public HttpCredentialCheck Check => check;

    public int ConnectionsStarted { get; private set; }

    public List<string> CredentialsVerified { get; } = [];

    public IReadOnlyList<string> CreateChallenges() => challenges;

    public IHttpCredentialVerifier StartConnection()
    {
        ConnectionsStarted++;

        return new Verifier(this);
    }

    private sealed class Verifier(ScriptedHttpAuthenticationMethod owner) : IHttpCredentialVerifier
    {
        public ValueTask<HttpCredentialCheck> VerifyAsync(
            string credentials, HttpAuthenticationRequest request, CancellationToken cancellationToken)
        {
            owner.CredentialsVerified.Add(credentials);

            return ValueTask.FromResult(owner.Check);
        }
    }
}
