using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// Proxy accounts that answer each password login as scripted and record every login asked: a
/// login with no user name gets <c>anonymous</c>, <c>alice</c> with <c>secret</c> gets
/// <c>alice</c>, and any other credentials get <c>refused</c>.
/// </summary>
internal sealed class ScriptedProxyAccounts(
    PasswordLoginVerdict anonymous = PasswordLoginVerdict.RefusedAnonymous,
    PasswordLoginVerdict refused = PasswordLoginVerdict.RefusedCredentials,
    PasswordLoginVerdict alice = PasswordLoginVerdict.Accepted) : IAuthenticationPolicy
{
    private readonly List<PasswordLogin> logins = [];

    public IReadOnlyList<PasswordLogin> Logins => logins;

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        logins.Add(login);
        if (login.UserName is null)
        {
            return ValueTask.FromResult(anonymous);
        }

        var isAlice = login.UserName == "alice" && login.Password!.Value.Span.SequenceEqual("secret"u8);

        return ValueTask.FromResult(isAlice ? alice : refused);
    }

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();
}
