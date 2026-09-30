using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

/// <summary>
/// An authentication policy double for the POP3 tests: it accepts the one account <c>u</c> with
/// password <c>p</c>, or answers every login with <see cref="Verdict"/> when that is set; answers
/// the login with no credentials with <see cref="AnonymousVerdict"/>; and offers a clear
/// password as <see cref="IsClearPasswordOffered"/> says, and no SASL mechanism or <c>APOP</c>.
/// </summary>
internal sealed class Pop3TestPolicy : IAuthenticationPolicy, IMailAuthenticationPolicy
{
    public PasswordLoginVerdict? Verdict { get; init; }

    public PasswordLoginVerdict AnonymousVerdict { get; init; } = PasswordLoginVerdict.RefusedAnonymous;

    public bool IsClearPasswordOffered { get; init; } = true;

    public List<PasswordLogin> Logins { get; } = [];

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        Logins.Add(login);
        var verdict = login.UserName is null ? AnonymousVerdict
            : Verdict ?? (login.UserName == "u" && login.Password is { } password && password.Span.SequenceEqual("p"u8)
                ? PasswordLoginVerdict.Accepted
                : PasswordLoginVerdict.RefusedCredentials);
        return ValueTask.FromResult(verdict);
    }

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();

    public MailLoginOffer GetMailLoginOffer(TlsSession? tlsSession) => new([], IsClearPasswordOffered, false);

    public ISaslExchange StartSaslExchange(SaslExchangeStart start) => throw new NotSupportedException();

    public ValueTask<MailLoginStep> CheckApopLoginAsync(ApopLogin login, CancellationToken cancellationToken) => throw new NotSupportedException();
}
