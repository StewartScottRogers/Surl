using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// A password-only policy as <c>Surl.Authentication</c>'s judges a password login (ADR-0032):
/// under <see cref="AllowsAnonymous"/> every login is accepted unchecked; otherwise a password
/// over plain text is refused unless <see cref="AllowsPlaintext"/>, no user is refused as
/// anonymous, and a user is accepted when its password is <see cref="Accounts"/>'.
/// </summary>
internal sealed class UnitTestAuthenticationPolicy : IAuthenticationPolicy
{
    public const string UserName = "tester";

    public const string Password = "secret";

    /// <summary>
    /// <c>Authorization</c> with <see cref="UserName"/> and <see cref="Password"/>.
    /// </summary>
    public const string BasicAuthorization = "Authorization: Basic dGVzdGVyOnNlY3JldA==";

    public Dictionary<string, string> Accounts { get; } = new(StringComparer.Ordinal) { [UserName] = Password };

    public bool AllowsAnonymous { get; set; }

    public bool AllowsPlaintext { get; set; } = true;

    public List<PasswordLogin> Logins { get; } = [];

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        Logins.Add(login);

        return ValueTask.FromResult(Judge(login));
    }

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();

    private PasswordLoginVerdict Judge(PasswordLogin login)
    {
        if (AllowsAnonymous)
        {
            return PasswordLoginVerdict.AcceptedUnchecked;
        }

        if (login.Password is not null && login.TlsSession is null && !AllowsPlaintext)
        {
            return PasswordLoginVerdict.RefusedPlaintext;
        }

        if (login.UserName is null)
        {
            return PasswordLoginVerdict.RefusedAnonymous;
        }

        return Accounts.TryGetValue(login.UserName, out var password)
            && password == System.Text.Encoding.UTF8.GetString(login.Password.GetValueOrDefault().Span)
            ? PasswordLoginVerdict.Accepted
            : PasswordLoginVerdict.RefusedCredentials;
    }
}
