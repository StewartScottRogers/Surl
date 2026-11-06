using System.Text.Json;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// <c>POST /register</c>: RFC 7591 dynamic client registration (ADR-0111 decision 6.3). Open to any caller, since a
/// client's tokens still need an account's password. Every client is public (<c>token_endpoint_auth_method</c>
/// <c>none</c>, RFC 7591 section 3.2.1 lets the server replace what a client asks), may use the
/// <c>authorization_code</c> and <c>refresh_token</c> grants, and needs at least one redirect URI, each
/// <c>https</c> or loopback <c>http</c>: else <c>400</c> <c>invalid_redirect_uri</c>. A body that is not a JSON
/// object, or is not sent as <c>application/json</c>, is <c>400</c> <c>invalid_client_metadata</c>.
/// </summary>
/// <param name="grants">What the authorization server has issued.</param>
/// <param name="writer">Writes the answers.</param>
internal sealed class McpClientRegistrationEndpoint(McpAuthorizationGrants grants, McpHttpAnswerWriter writer)
{
    private static readonly HttpStatus Created = new(201, "Created");

    /// <summary>Answers a registration.</summary>
    /// <param name="request">The request.</param>
    /// <param name="body">Its body, read whole.</param>
    /// <returns>The answer.</returns>
    public HttpAnswer Answer(HttpRequest request, byte[] body)
    {
        if (!McpHttpFields.IsMediaType(McpHttpFields.Field(request, "Content-Type"), McpHttpAnswerWriter.JsonMediaType)
            || ParseObject(body) is not { } metadata)
        {
            return Error(request, "invalid_client_metadata");
        }

        if (RedirectUrisOf(metadata) is not { } redirectUris)
        {
            return Error(request, "invalid_redirect_uri");
        }

        string? clientName = metadata.TryGetProperty("client_name", out JsonElement name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null;
        return grants.RegisterClient(redirectUris, clientName) is { } registered
            ? writer.NoStoreJson(request, Created, McpOAuthJson.WriteRegistration(registered.ClientId, registered.Client))
            : writer.Empty(request, HttpStatus.ServiceUnavailable, [new("Retry-After", "60")]);
    }

    private HttpAnswer Error(HttpRequest request, string error) =>
        writer.NoStoreJson(request, HttpStatus.BadRequest, McpOAuthJson.WriteError(error));

    private static JsonElement? ParseObject(byte[] body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string[]? RedirectUrisOf(JsonElement metadata)
    {
        if (!metadata.TryGetProperty("redirect_uris", out JsonElement uris) || uris.ValueKind != JsonValueKind.Array || uris.GetArrayLength() == 0)
        {
            return null;
        }

        string[] values = [.. uris.EnumerateArray().Select(uri => uri.ValueKind == JsonValueKind.String ? uri.GetString()! : string.Empty)];
        return values.All(McpRedirectUri.IsAllowed) ? values : null;
    }
}
