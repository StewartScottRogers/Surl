using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// An <see cref="IAuthenticationPolicy"/> with no <c>--allow-anonymous</c>: every login that
/// carries no credentials is <see cref="PasswordLoginVerdict.RefusedAnonymous"/>, so <c>MAIL</c>
/// needs an <c>AUTH</c> first. It records each login it was asked about.
/// </summary>
internal sealed class UnitTestRefusingPolicy : IAuthenticationPolicy
{
    public List<PasswordLogin> Logins { get; } = [];

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        Logins.Add(login);
        return ValueTask.FromResult(PasswordLoginVerdict.RefusedAnonymous);
    }

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();
}
