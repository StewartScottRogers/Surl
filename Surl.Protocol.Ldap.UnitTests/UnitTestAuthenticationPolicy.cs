using System.Collections.Concurrent;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// An <see cref="IAuthenticationPolicy"/> that answers every password login with one verdict,
/// and every login with no credentials - a bind's anonymous check, or the server asking
/// whether an unbound connection may read - with another, recording each login it judged.
/// </summary>
/// <param name="passwordVerdict">The verdict for a login that carries a password.</param>
/// <param name="anonymousVerdict">The verdict for a login with neither user name nor password.</param>
internal sealed class UnitTestAuthenticationPolicy(
    PasswordLoginVerdict passwordVerdict,
    PasswordLoginVerdict anonymousVerdict = PasswordLoginVerdict.RefusedAnonymous) : IAuthenticationPolicy
{
    private readonly ConcurrentQueue<PasswordLogin> logins = new();

    public IReadOnlyList<PasswordLogin> Logins => [.. logins];

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        // A null password stays null: a conditional with a byte[] arm would turn it into empty memory.
        ReadOnlyMemory<byte>? passwordCopy = null;
        if (login.Password is { } password)
        {
            passwordCopy = password.ToArray();
        }

        logins.Enqueue(login with { Password = passwordCopy });

        var isAnonymous = login.UserName is null && login.Password is null;

        return ValueTask.FromResult(isAnonymous ? anonymousVerdict : passwordVerdict);
    }

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) =>
        throw new NotSupportedException("The LDAP server never starts an HTTP authentication session.");
}
