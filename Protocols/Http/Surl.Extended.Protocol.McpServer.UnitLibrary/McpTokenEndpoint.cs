using System.Text;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// <c>POST /token</c> (ADR-0111 decision 6.3; OAuth 2.1 section 3.2), its body
/// <c>application/x-www-form-urlencoded</c>. Three grants:
/// <list type="bullet">
/// <item><c>authorization_code</c>: the code, used once, with the <c>client_id</c> it was issued to, the
/// <c>redirect_uri</c> it was sent to when one is given, and a <c>code_verifier</c> matching its <c>S256</c>
/// challenge; anything else <c>invalid_grant</c>. Answered with an access token and a refresh token.</item>
/// <item><c>refresh_token</c>: the refresh token, used once and replaced (rotated), with the <c>client_id</c> it was
/// issued to; else <c>invalid_grant</c>.</item>
/// <item><c>client_credentials</c>: the client is an account, by HTTP Basic or <c>client_id</c> and
/// <c>client_secret</c>; refused <c>401</c> <c>invalid_client</c>. Answered with an access token only.</item>
/// </list>
/// Every token is bound to the <c>resource</c> (RFC 8707): this endpoint when none is given, and
/// <c>invalid_target</c> when another is. Errors are <c>400</c> with RFC 6749 section 5.2's object.
/// </summary>
/// <param name="grants">What the authorization server has issued.</param>
/// <param name="passwords">Checks a <c>client_credentials</c> client's account.</param>
/// <param name="writer">Writes the answers.</param>
internal sealed class McpTokenEndpoint(McpAuthorizationGrants grants, McpPasswordCheck passwords, McpHttpAnswerWriter writer)
{
    private const string FormMediaType = "application/x-www-form-urlencoded";

    /// <summary>Answers a token request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="body">Its body, read whole.</param>
    /// <param name="resource">This endpoint's resource, <c>&lt;scheme&gt;://&lt;authority&gt;&lt;endpoint&gt;</c>.</param>
    /// <param name="cancellationToken">Cancels the login check.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, byte[] body, string resource, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string>? form = McpHttpFields.IsMediaType(McpHttpFields.Field(request, "Content-Type"), FormMediaType)
            ? McpOAuthForm.Parse(Encoding.UTF8.GetString(body))
            : null;
        if (form is null)
        {
            return Error(request, "invalid_request");
        }

        if (form.TryGetValue("resource", out string? asked) && asked != resource)
        {
            return Error(request, "invalid_target");
        }

        return form.GetValueOrDefault("grant_type") switch
        {
            "authorization_code" => RedeemCode(request, form),
            "refresh_token" => Refresh(request, form),
            "client_credentials" => await GrantClientCredentialsAsync(request, form, resource, cancellationToken).ConfigureAwait(false),
            null => Error(request, "invalid_request"),
            _ => Error(request, "unsupported_grant_type"),
        };
    }

    private HttpAnswer RedeemCode(HttpRequest request, IReadOnlyDictionary<string, string> form)
    {
        McpAuthorizationCode? code = grants.RedeemCode(form.GetValueOrDefault("code") ?? string.Empty);
        return code is not null && IsRedeemableBy(code, form)
            ? Issue(request, new McpIssuedToken(code.ClientId, code.Resource, code.Account), withRefreshToken: true)
            : Error(request, "invalid_grant");
    }

    private static bool IsRedeemableBy(McpAuthorizationCode code, IReadOnlyDictionary<string, string> form) =>
        form.GetValueOrDefault("client_id") == code.ClientId
        && (!form.TryGetValue("redirect_uri", out string? redirect) || redirect == code.RedirectUri)
        && McpPkce.Verifies(form.GetValueOrDefault("code_verifier") ?? string.Empty, code.CodeChallenge);

    private HttpAnswer Refresh(HttpRequest request, IReadOnlyDictionary<string, string> form)
    {
        McpIssuedToken? token = grants.RedeemRefreshToken(form.GetValueOrDefault("refresh_token") ?? string.Empty);
        return token is not null && form.GetValueOrDefault("client_id") == token.ClientId
            ? Issue(request, token, withRefreshToken: true)
            : Error(request, "invalid_grant");
    }

    private async Task<HttpAnswer> GrantClientCredentialsAsync(
        HttpRequest request, IReadOnlyDictionary<string, string> form, string resource, CancellationToken cancellationToken)
    {
        (string UserName, string Password)? credentials = McpPasswordCheck.BasicCredentialsOf(McpHttpFields.Field(request, "Authorization"))
            ?? (form.TryGetValue("client_id", out string? id) && form.TryGetValue("client_secret", out string? secret) ? (id, secret) : null);
        string? account = credentials is { } given
            ? await passwords.AccountOfAsync(given.UserName, given.Password, cancellationToken).ConfigureAwait(false)
            : null;
        if (account is null)
        {
            return writer.NoStoreJson(
                request, HttpStatus.Unauthorized, McpOAuthJson.WriteError("invalid_client"), [new("WWW-Authenticate", McpPasswordCheck.BasicChallenge)]);
        }

        return Issue(request, new McpIssuedToken(null, resource, account), withRefreshToken: false);
    }

    private HttpAnswer Issue(HttpRequest request, McpIssuedToken grant, bool withRefreshToken) =>
        grants.IssueTokens(grant, withRefreshToken) is { } tokens
            ? writer.NoStoreJson(request, HttpStatus.Ok, McpOAuthJson.WriteTokens(tokens))
            : writer.Empty(request, HttpStatus.ServiceUnavailable, [new("Retry-After", "60")]);

    private HttpAnswer Error(HttpRequest request, string error) =>
        writer.NoStoreJson(request, HttpStatus.BadRequest, McpOAuthJson.WriteError(error));
}
