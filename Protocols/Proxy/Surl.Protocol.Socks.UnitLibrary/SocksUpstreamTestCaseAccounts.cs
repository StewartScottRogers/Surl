using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The account an upstream test case's <c>user</c> and <c>password</c> server commands name: it
/// refuses a login with no user name, so the listener selects method <c>02</c>, and accepts the
/// named credentials, or any when the case names none.
/// </summary>
/// <param name="user">The one user name accepted, or <see langword="null"/> for any.</param>
/// <param name="password">The one password accepted, or <see langword="null"/> for any.</param>
internal sealed class SocksUpstreamTestCaseAccounts(string? user, string? password) : IAuthenticationPolicy
{
    /// <inheritdoc/>
    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);

        return ValueTask.FromResult(JudgeLogin(login));
    }

    /// <inheritdoc/>
    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();

    private PasswordLoginVerdict JudgeLogin(PasswordLogin login)
    {
        if (login.UserName is null)
        {
            return PasswordLoginVerdict.RefusedAnonymous;
        }

        var isAccepted = user is null
            || (login.UserName == user && login.Password is { } sent && sent.Span.SequenceEqual(Encoding.UTF8.GetBytes(password ?? string.Empty)));

        return isAccepted ? PasswordLoginVerdict.Accepted : PasswordLoginVerdict.RefusedCredentials;
    }
}
