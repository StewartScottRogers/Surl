using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// <c>GET /authorize</c>: the authorization-code grant's first half, with PKCE <c>S256</c> required (ADR-0111 decision
/// 6.3; OAuth 2.1 section 4.1). In order:
/// <list type="number">
/// <item>An unknown <c>client_id</c>, or a <c>redirect_uri</c> the client did not register (or none, when it registered
/// several), is <c>400</c> <c>invalid_request</c> and never redirected (OAuth 2.1 section 4.1.2.1); so is a parameter
/// given twice.</item>
/// <item>A <c>response_type</c> other than <c>code</c>, a missing <c>code_challenge</c> or a method other than
/// <c>S256</c>, or a <c>resource</c> (RFC 8707) other than this endpoint, is redirected with
/// <c>unsupported_response_type</c>, <c>invalid_request</c> or <c>invalid_target</c>.</item>
/// <item>The resource owner logs in with HTTP Basic against Surl's accounts; without a login the policy accepts it is
/// <c>401</c> with a Basic challenge, so a browser shows its own prompt.</item>
/// <item>The code is redirected with the request's <c>state</c>.</item>
/// </list>
/// </summary>
/// <param name="grants">What the authorization server has issued.</param>
/// <param name="passwords">Checks the resource owner's login.</param>
/// <param name="writer">Writes the answers.</param>
internal sealed class McpAuthorizeEndpoint(McpAuthorizationGrants grants, McpPasswordCheck passwords, McpHttpAnswerWriter writer)
{
    private static readonly HttpStatus Found = new(302, "Found");

    /// <summary>Answers an authorization request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="resource">This endpoint's resource, <c>&lt;scheme&gt;://&lt;authority&gt;&lt;endpoint&gt;</c>.</param>
    /// <param name="cancellationToken">Cancels the login check.</param>
    /// <returns>The answer.</returns>
    public async Task<HttpAnswer> AnswerAsync(HttpRequest request, string resource, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string>? query = McpOAuthForm.Parse(McpOAuthForm.QueryOf(request.Target));
        if (query is null || RedirectUriOf(query) is not { } redirect)
        {
            return writer.NoStoreJson(request, HttpStatus.BadRequest, McpOAuthJson.WriteError("invalid_request"));
        }

        string? state = query.GetValueOrDefault("state");
        if (RequestError(query, resource) is { } error)
        {
            return Redirect(request, McpRedirectUri.WithParameters(redirect, ("error", error), ("state", state)));
        }

        string? account = await passwords.AccountOfBasicAsync(McpHttpFields.Field(request, "Authorization"), cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return writer.Empty(request, HttpStatus.Unauthorized, [new("WWW-Authenticate", McpPasswordCheck.BasicChallenge)]);
        }

        McpAuthorizationCode grant = new(query["client_id"], redirect, query["code_challenge"], resource, account);
        return grants.IssueCode(grant) is { } code
            ? Redirect(request, McpRedirectUri.WithParameters(redirect, ("code", code), ("state", state)))
            : Redirect(request, McpRedirectUri.WithParameters(redirect, ("error", "temporarily_unavailable"), ("state", state)));
    }

    // The client and the redirect URI are checked before anything is redirected anywhere.
    private string? RedirectUriOf(IReadOnlyDictionary<string, string> query)
    {
        if (grants.FindClient(query.GetValueOrDefault("client_id") ?? string.Empty) is not { } client)
        {
            return null;
        }

        if (query.TryGetValue("redirect_uri", out string? redirect))
        {
            return client.RedirectUris.Contains(redirect, StringComparer.Ordinal) ? redirect : null;
        }

        return client.RedirectUris.Count == 1 ? client.RedirectUris[0] : null;
    }

    private static string? RequestError(IReadOnlyDictionary<string, string> query, string resource)
    {
        if (query.GetValueOrDefault("response_type") != "code")
        {
            return "unsupported_response_type";
        }

        if (!query.ContainsKey("code_challenge") || query.GetValueOrDefault("code_challenge_method") != McpPkce.Method)
        {
            return "invalid_request";
        }

        return query.TryGetValue("resource", out string? asked) && asked != resource ? "invalid_target" : null;
    }

    private HttpAnswer Redirect(HttpRequest request, string location) =>
        writer.Empty(request, Found, [new("Location", location), new("Cache-Control", "no-store")]);
}
