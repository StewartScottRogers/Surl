using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Checks an account name and password against Surl's accounts through the policy's password login (ADR-0111
/// decision 6.3; ADR-0032 section 6): the resource owner at <c>/authorize</c>, sent by HTTP Basic, and the client of
/// a <c>client_credentials</c> grant, sent by HTTP Basic or as <c>client_id</c> and <c>client_secret</c>. A clear
/// password over plain <c>mcp</c> is refused unless <c>--allow-plaintext-auth</c> is given, as the policy decides.
/// </summary>
/// <param name="policy">The policy that judges password logins.</param>
/// <param name="scheme">The listen scheme, for the log.</param>
/// <param name="tlsSession">The connection's TLS session; <see langword="null" /> for plain <c>mcp</c>.</param>
internal sealed class McpPasswordCheck(IAuthenticationPolicy policy, string scheme, TlsSession? tlsSession)
{
    /// <summary>The challenge a <c>401</c> for a missing or refused Basic login carries.</summary>
    public const string BasicChallenge = "Basic realm=\"surl\", charset=\"UTF-8\"";

    /// <summary>The account an <c>Authorization: Basic</c> field logs in as.</summary>
    /// <param name="authorization">The field's value, or <see langword="null" /> when the request sent none.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The account, or <see langword="null" /> when the field is absent, not Basic, unreadable or refused.</returns>
    public async Task<string?> AccountOfBasicAsync(string? authorization, CancellationToken cancellationToken) =>
        BasicCredentialsOf(authorization) is { } credentials
            ? await AccountOfAsync(credentials.UserName, credentials.Password, cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>The account a name and password log in as.</summary>
    /// <param name="userName">The account name.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The account, or <see langword="null" /> unless the policy accepted the credentials it checked.</returns>
    public async Task<string?> AccountOfAsync(string userName, string password, CancellationToken cancellationToken)
    {
        PasswordLogin login = new(scheme, userName, Encoding.UTF8.GetBytes(password), tlsSession);
        PasswordLoginVerdict verdict = await policy.CheckPasswordLoginAsync(login, cancellationToken).ConfigureAwait(false);
        return verdict == PasswordLoginVerdict.Accepted ? userName : null;
    }

    /// <summary>The name and password of an <c>Authorization: Basic</c> field (RFC 7617).</summary>
    /// <param name="authorization">The field's value, or <see langword="null" />.</param>
    /// <returns>The credentials, or <see langword="null" /> when the field is absent, not Basic or unreadable.</returns>
    public static (string UserName, string Password)? BasicCredentialsOf(string? authorization)
    {
        const string prefix = "Basic ";
        if (authorization is null || !authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        byte[] decoded = new byte[authorization.Length];
        if (!Convert.TryFromBase64String(authorization[prefix.Length..].Trim(), decoded, out int length))
        {
            return null;
        }

        string text = Encoding.UTF8.GetString(decoded, 0, length);
        int colon = text.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? null : (text[..colon], text[(colon + 1)..]);
    }
}
