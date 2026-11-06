using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Judges one connection's <c>AUTH</c> and <c>HELLO … AUTH</c> logins through the policy
/// (ADR-0106 decision 4, ADR-0100 decision 4.1).
/// </summary>
/// <param name="authenticationPolicy">Judges the login, waiting its refusal delay on a refusal.</param>
/// <param name="context">The exchange's context: its scheme, log and cancellation token.</param>
/// <param name="tlsSession">The connection's TLS session; <see langword="null"/> means unencrypted.</param>
internal sealed class RedisLogins(IAuthenticationPolicy authenticationPolicy, ExchangeContext context, TlsSession? tlsSession)
{
    /// <summary>
    /// Logs <paramref name="session"/> in as <paramref name="userName"/> when the policy accepts
    /// the login; a refusal leaves an earlier login in force and notes its cause in the log only.
    /// </summary>
    /// <param name="session">The connection's state.</param>
    /// <param name="userName">The account name's bytes, UTF-8.</param>
    /// <param name="password">The password's bytes.</param>
    /// <returns>Whether the login was accepted.</returns>
    public async ValueTask<bool> TryLogInAsync(RedisSession session, byte[] userName, byte[] password)
    {
        var user = Encoding.UTF8.GetString(userName);
        var verdict = await authenticationPolicy.CheckPasswordLoginAsync(
            new PasswordLogin(context.Scheme, user, password, tlsSession), context.CancellationToken);
        if (verdict is PasswordLoginVerdict.Accepted or PasswordLoginVerdict.AcceptedUnchecked)
        {
            session.LogIn(user);
            return true;
        }

        context.Log.Note($"A login was refused ({verdict}); answered -WRONGPASS.");
        return false;
    }
}
