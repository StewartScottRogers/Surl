using System.Globalization;
using System.Text;
using Surl.HttpMessage;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Answers <c>GET /token</c>, the registry's own token endpoint (Token "Requesting a Token",
/// "Token Response Fields"; ADR-0115, decision 4.3): checks the <c>Basic</c> credentials through
/// <see cref="IAuthenticationPolicy"/>, grants each asked scope what the login allows, and issues
/// an opaque token from <see cref="OciTokenBook"/>.
/// </summary>
/// <remarks>
/// An account is granted <c>pull</c>, and <c>push</c> and <c>delete</c> under
/// <c>--allow-uploads</c>; the share's access is applied by the content store on each request the
/// token serves. A request with no credentials gets an anonymous <c>pull</c>-only token when the
/// policy allows anonymous logins (<c>--allow-anonymous</c>), and a login the policy accepts
/// unchecked is anonymous too, so an anonymous token never grants <c>push</c>.
/// </remarks>
/// <param name="policy">Checks the credentials and says whether anonymous logins are allowed.</param>
/// <param name="tokens">The server's tokens.</param>
/// <param name="allowsUploads">Whether the store allows uploads, so a token may grant <c>push</c> and <c>delete</c>.</param>
/// <param name="tlsSession">The connection's TLS session, or <see langword="null"/> on plain text.</param>
/// <param name="context">The exchange: its random source.</param>
internal sealed class OciTokenEndpoint(IAuthenticationPolicy policy, OciTokenBook tokens, bool allowsUploads, TlsSession? tlsSession, ExchangeContext context)
{
    /// <summary>
    /// The endpoint's path.
    /// </summary>
    public const string Path = "/token";

    /// <summary>
    /// How many scopes one token request is granted; the rest are dropped (ADR-0115, decision 7).
    /// </summary>
    public const int MaxScopes = 16;

    private const string BasicPrefix = "Basic ";

    /// <summary>
    /// Whether <paramref name="target"/> names the token endpoint, with or without a query.
    /// </summary>
    /// <param name="target">The request target.</param>
    /// <returns><see langword="true"/> for <c>/token</c>.</returns>
    public static bool IsTokenTarget(string target)
    {
        var queryStart = target.IndexOf('?');

        return (queryStart < 0 ? target : target[..queryStart]) == Path;
    }

    /// <summary>
    /// Answers a request to the token endpoint.
    /// </summary>
    /// <param name="reply">The reply to the request.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the login check.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(OciReply reply, HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.Method != "GET")
        {
            return reply.Error(OciError.UnsupportedMethod, new KeyValuePair<string, string>("Allow", "GET"));
        }

        var queryStart = request.Target.IndexOf('?');
        var query = queryStart < 0 ? string.Empty : request.Target[(queryStart + 1)..];
        if (OciQueryParameters.First(query, "service") is { } service && service != OciChallenge.Service)
        {
            return reply.Error(OciError.UnsupportedParameter);
        }

        var (isLoggedIn, accountName, refusal) = await LogInAsync(OciRequestFields.Value(request, "Authorization"), cancellationToken);
        if (!isLoggedIn)
        {
            return refusal == OciError.Unauthorized
                ? reply.Error(OciError.Unauthorized, new KeyValuePair<string, string>("WWW-Authenticate", OciChallenge.Basic))
                : reply.Error(refusal!);
        }

        var granted = accountName is null ? OciActions.Pull : AccountActions();

        return tokens.Issue(accountName, Grants(query, granted), context.Identifiers) is { } token
            ? reply.Bytes(HttpStatus.Ok, "application/json", [], TokenBody(token))
            : reply.Error(OciError.TooManyRequests);
    }

    private OciActions AccountActions() =>
        allowsUploads ? OciActions.Pull | OciActions.Push | OciActions.Delete : OciActions.Pull;

    // Logged in as an account, or anonymously (account null); or refused with the error to send.
    private async Task<(bool IsLoggedIn, string? AccountName, OciError? Refusal)> LogInAsync(string? authorization, CancellationToken cancellationToken)
    {
        PasswordLogin login;
        if (authorization is null)
        {
            login = new PasswordLogin(OciRegistryProtocolServer.Scheme, null, null, tlsSession);
        }
        else if (ReadBasic(authorization) is { } credentials)
        {
            login = new PasswordLogin(OciRegistryProtocolServer.Scheme, credentials.UserName, credentials.Password, tlsSession);
        }
        else
        {
            return (false, null, OciError.Unauthorized);
        }

        return await policy.CheckPasswordLoginAsync(login, cancellationToken) switch
        {
            PasswordLoginVerdict.Accepted => (true, login.UserName, null),
            PasswordLoginVerdict.AcceptedUnchecked => (true, null, null),
            PasswordLoginVerdict.RefusedPlaintext => (false, null, OciError.PlaintextCredentialsRefused),
            _ => (false, null, OciError.Unauthorized),
        };
    }

    // RFC 7617: base64 of user-id ":" password, UTF-8; anything else is no credentials at all.
    private static (string UserName, ReadOnlyMemory<byte> Password)? ReadBasic(string authorization)
    {
        if (!authorization.StartsWith(BasicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var bytes = new byte[authorization.Length];
        if (!Convert.TryFromBase64String(authorization[BasicPrefix.Length..].Trim(), bytes, out var length))
        {
            return null;
        }

        var colon = Array.IndexOf(bytes, (byte)':', 0, length);

        return colon < 0
            ? null
            : (Encoding.UTF8.GetString(bytes, 0, colon), bytes.AsMemory(colon + 1, length - colon - 1));
    }

    // Each asked scope, past the first MaxScopes dropped, granted what the login allows; a
    // repository named twice is granted the union.
    private static Dictionary<string, OciActions> Grants(string query, OciActions granted)
    {
        var grants = new Dictionary<string, OciActions>(StringComparer.Ordinal);
        var scopes = OciQueryParameters.All(query, "scope")
            .SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Take(MaxScopes);
        foreach (var scope in scopes)
        {
            if (OciTokenScope.Parse(scope) is { } asked && (asked.Actions & granted) is var actions and not OciActions.None)
            {
                grants[asked.Name] = grants.GetValueOrDefault(asked.Name) | actions;
            }
        }

        return grants;
    }

    private byte[] TokenBody(string token)
    {
        var issuedAt = context.TimeProvider.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var expiresIn = ((int)OciTokenBook.Lifetime.TotalSeconds).ToString(CultureInfo.InvariantCulture);

        return Encoding.UTF8.GetBytes($"{{\"token\":\"{token}\",\"access_token\":\"{token}\",\"expires_in\":{expiresIn},\"issued_at\":\"{issuedAt}\"}}");
    }
}
