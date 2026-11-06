using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

/// <summary>
/// Accepts <c>alice</c> with <c>secret</c> and the token <c>s3cret</c> (the empty-name account's
/// password); refuses no credentials as anonymous and anything else as wrong credentials, unless
/// <see cref="Verdict"/> says what to answer every login.
/// </summary>
internal sealed class UnitTestNatsAuthenticationPolicy : IAuthenticationPolicy
{
    public List<PasswordLogin> Logins { get; } = [];

    public PasswordLoginVerdict? Verdict { get; init; }

    public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken)
    {
        Logins.Add(login);
        return ValueTask.FromResult(Verdict ?? Judge(login));
    }

    public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();

    public static string PasswordOf(PasswordLogin login) =>
        login.Password is { } password ? Encoding.UTF8.GetString(password.Span) : "(none)";

    private static PasswordLoginVerdict Judge(PasswordLogin login) => (login.UserName, PasswordOf(login)) switch
    {
        (null, _) => PasswordLoginVerdict.RefusedAnonymous,
        ("alice", "secret") or ("", "s3cret") => PasswordLoginVerdict.Accepted,
        _ => PasswordLoginVerdict.RefusedCredentials,
    };
}
