using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A hand-written policy for the server's tests: lets an anonymous caller in only when told to,
/// and accepts two accounts, <c>default</c> with password <c>hunter2</c> and <c>alice</c> with
/// <c>secret</c>; any other login is <see cref="RefusalVerdict"/>. Under
/// <c>allowsAnonymous</c> every login is accepted unchecked, as <c>--allow-anonymous</c> does.
/// </summary>
internal sealed class UnitTestRedisAuthenticationPolicy(bool allowsAnonymous) : IAuthenticationPolicy
{
    public List<PasswordLogin> Logins { get; } = [];

    public PasswordLoginVerdict RefusalVerdict { get; init; } = PasswordLoginVerdict.RefusedCredentials;

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        Logins.Add(login);
        return ValueTask.FromResult(Judge(login));
    }

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();

    private PasswordLoginVerdict Judge(PasswordLogin login)
    {
        if (allowsAnonymous)
        {
            return PasswordLoginVerdict.AcceptedUnchecked;
        }

        if (login.UserName is null)
        {
            return PasswordLoginVerdict.RefusedAnonymous;
        }

        var password = Encoding.UTF8.GetString(login.Password.GetValueOrDefault().Span);
        var isKnown = (login.UserName, password) is ("default", "hunter2") or ("alice", "secret");
        return isKnown ? PasswordLoginVerdict.Accepted : RefusalVerdict;
    }
}
