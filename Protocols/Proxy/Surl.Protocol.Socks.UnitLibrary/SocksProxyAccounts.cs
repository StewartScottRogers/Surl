using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// Asks the listener's proxy accounts whether a client that names no account may use the proxy
/// (ADR-0121 decisions 2, 3 and 4).
/// </summary>
internal static class SocksProxyAccounts
{
    /// <summary>
    /// Whether a client may use the proxy without an account: always when the listener has no
    /// proxy accounts, and otherwise only when the policy accepts a login with no user name and no
    /// password unchecked (<c>--allow-anonymous</c>). The question carries no credentials, so the
    /// policy neither delays nor records it.
    /// </summary>
    /// <param name="accounts">The listener's proxy accounts, or <see langword="null"/> when it has none.</param>
    /// <param name="client">curl's connection, for its encryption.</param>
    /// <param name="context">The exchange, for its scheme.</param>
    /// <param name="cancellationToken">Cancels the question.</param>
    /// <returns><see langword="true"/> when no account is needed.</returns>
    public static async ValueTask<bool> AllowAnonymousAsync(
        IAuthenticationPolicy? accounts, IConnection client, ExchangeContext context, CancellationToken cancellationToken)
    {
        if (accounts is null)
        {
            return true;
        }

        var verdict = await accounts.CheckPasswordLoginAsync(new PasswordLogin(context.Scheme, null, null, client.TlsSession), cancellationToken);

        return verdict == PasswordLoginVerdict.AcceptedUnchecked;
    }
}
