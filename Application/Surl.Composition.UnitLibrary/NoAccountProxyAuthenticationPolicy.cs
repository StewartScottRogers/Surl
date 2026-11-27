using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The proxy policy of a start with no proxy account (ADR-0088 decision 5): no login is asked for,
/// so every password login and HTTP request is answered as <see cref="AnonymousAuthenticationPolicy"/>
/// answers it, while a Kerberos login the SOCKS5 server has checked against <c>--keytab</c> is judged
/// by <paramref name="kerberosLogins"/>, which publishes it and applies <c>refuse-login</c> fault rules
/// (ADR-0165, decisions 7 to 9).
/// </summary>
/// <param name="kerberosLogins">
/// The policy that judges each Kerberos login. It must match no account, since with no proxy account
/// every principal the keytab's keys accept stands.
/// </param>
internal sealed class NoAccountProxyAuthenticationPolicy(IAuthenticationPolicy kerberosLogins) : IAuthenticationPolicy
{
    private readonly AnonymousAuthenticationPolicy noLogin = new();

    /// <inheritdoc/>
    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken) =>
        noLogin.CheckPasswordLoginAsync(login, cancellationToken);

    /// <inheritdoc/>
    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => noLogin.StartHttpConnection(tlsSession);

    /// <inheritdoc/>
    public ValueTask<bool> JudgeKerberosLoginAsync(KerberosLogin login, CancellationToken cancellationToken) =>
        kerberosLogins.JudgeKerberosLoginAsync(login, cancellationToken);
}
