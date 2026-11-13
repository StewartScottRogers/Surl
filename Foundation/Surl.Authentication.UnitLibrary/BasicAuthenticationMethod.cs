using System.Text;
using System.Text.Unicode;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// HTTP Basic, RFC 7617 (ADR-0032, sections 3 and 4): the challenge
/// <c>Basic realm="surl", charset="UTF-8"</c>, and the check of the <c>user-id:password</c>
/// that upstream curl's <c>-u</c> sends base64-encoded. The user-id is split from the password
/// at the first <c>:</c>, the user-id read as UTF-8 and the password compared as the bytes sent.
/// Basic holds no per-connection state, so every connection shares this one verifier.
/// </summary>
public sealed class BasicAuthenticationMethod : IHttpAuthenticationMethod, IHttpCredentialVerifier
{
    /// <summary>
    /// The one <c>WWW-Authenticate</c> value offering Basic (ADR-0032, section 4).
    /// </summary>
    public const string Challenge = "Basic realm=\"surl\", charset=\"UTF-8\"";

    private static readonly HttpCredentialCheck Refused = new(HttpCredentialOutcome.Refused, null, []);

    private readonly AccountBookAndDirectoryPasswordCheck passwordCheck;

    /// <summary>
    /// Checks Basic credentials against <paramref name="accounts"/>.
    /// </summary>
    /// <param name="accounts">The configured accounts.</param>
    public BasicAuthenticationMethod(AccountBook accounts)
        : this(accounts, null)
    {
    }

    private BasicAuthenticationMethod(AccountBook accounts, LdapAccountSource? directory)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        passwordCheck = new AccountBookAndDirectoryPasswordCheck(accounts, directory);
    }

    /// <summary>
    /// This method, also asking <paramref name="directory"/> about a name the account book does not hold
    /// (ADR-0149, decision 2).
    /// </summary>
    /// <param name="directory">The LDAP account source.</param>
    /// <returns>The method checking against the book and the directory.</returns>
    internal BasicAuthenticationMethod WithDirectory(LdapAccountSource directory) => new(passwordCheck.Accounts, directory);

    /// <inheritdoc/>
    public AuthenticationMethod Method => AuthenticationMethod.Basic;

    /// <inheritdoc/>
    public IReadOnlyList<string> CreateChallenges() => [Challenge];

    /// <inheritdoc/>
    public IHttpCredentialVerifier StartConnection() => this;

    /// <summary>
    /// Decodes <paramref name="credentials"/> as base64 and checks the <c>user-id:password</c>
    /// inside. A value that is not base64, holds no <c>:</c>, or whose user-id is not UTF-8 is
    /// refused, never thrown; a user-id that is not UTF-8 still costs one password comparison,
    /// like an unknown one (ADR-0032, section 8).
    /// </summary>
    /// <param name="credentials">The base64 text after <c>Basic</c>.</param>
    /// <param name="request">The request the field arrived on; not read.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Accepted as the user-id's account, or refused.</returns>
    public async ValueTask<HttpCredentialCheck> VerifyAsync(
        string credentials, HttpAuthenticationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        cancellationToken.ThrowIfCancellationRequested();

        var decoded = new byte[credentials.Length];
        if (!Convert.TryFromBase64String(credentials, decoded, out var length))
        {
            return Refused;
        }

        var userPass = decoded.AsSpan(0, length);
        var colon = userPass.IndexOf((byte)':');
        if (colon < 0)
        {
            return Refused;
        }

        var userIdBytes = userPass[..colon];
        var userId = Utf8.IsValid(userIdBytes) ? Encoding.UTF8.GetString(userIdBytes) : null;

        var password = userPass[(colon + 1)..].ToArray();
        var isAccepted = await passwordCheck
            .CheckAsync(AuthenticationPolicy.HttpLoginScheme, userId, password, cancellationToken).ConfigureAwait(false);

        return isAccepted
            ? new HttpCredentialCheck(HttpCredentialOutcome.Accepted, userId, [], userId)
            : Refused with { UserAsSent = userId };
    }
}
