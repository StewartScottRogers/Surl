using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// A login policy whose answers a test sets: the verdict for a login with a password, the
/// verdict for the login that carries none, and whether the clear-password login is offered.
/// It records every password login it is asked about.
/// </summary>
internal sealed class ScriptedLoginPolicy(
    PasswordLoginVerdict passwordVerdict = PasswordLoginVerdict.Accepted,
    PasswordLoginVerdict anonymousVerdict = PasswordLoginVerdict.RefusedAnonymous,
    bool isClearPasswordLoginOffered = true) : IAuthenticationPolicy, IMailAuthenticationPolicy
{
    public List<PasswordLogin> Logins { get; } = [];

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        Logins.Add(login);
        return ValueTask.FromResult(login.Password is null ? anonymousVerdict : passwordVerdict);
    }

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();

    public MailLoginOffer GetMailLoginOffer(TlsSession? tlsSession) => new([], isClearPasswordLoginOffered, false);

    public ISaslExchange StartSaslExchange(SaslExchangeStart start) => throw new NotSupportedException();

    public ValueTask<MailLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken) => throw new NotSupportedException();
}
