using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// A hand-written policy for the server's tests, judging as <c>Surl.Authentication</c> does for an
/// account set of the bearer token <see cref="Token"/> (the empty-name account) and
/// <see cref="UserName"/> with <see cref="Password"/>: a login without credentials is
/// <c>AcceptedUnchecked</c> only when <paramref name="allowsAnonymous"/>, and anything else is
/// refused. It records every login it judges.
/// </summary>
internal sealed class UnitTestPasswordPolicy(bool allowsAnonymous) : IAuthenticationPolicy
{
    public const string Token = "secret-token";

    public const string UserName = "alice";

    public const string Password = "wonderland";

    public List<PasswordLogin> Logins { get; } = [];

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        lock (Logins)
        {
            Logins.Add(login);
        }

        var password = login.Password is { } bytes ? Encoding.UTF8.GetString(bytes.Span) : null;
        var verdict = (login.UserName, password) switch
        {
            (null, _) => allowsAnonymous ? PasswordLoginVerdict.AcceptedUnchecked : PasswordLoginVerdict.RefusedAnonymous,
            ("", Token) => PasswordLoginVerdict.Accepted,
            (UserName, Password) => PasswordLoginVerdict.Accepted,
            _ => PasswordLoginVerdict.RefusedCredentials,
        };

        return ValueTask.FromResult(verdict);
    }

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();
}
