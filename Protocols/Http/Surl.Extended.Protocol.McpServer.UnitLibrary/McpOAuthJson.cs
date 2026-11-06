using System.Buffers;
using System.Text.Json;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The JSON documents the authorization server writes (ADR-0111 decision 6.3): its RFC 8414 metadata, an RFC 7591
/// registration, an RFC 6749 section 5.1 token response and an error object (RFC 6749 section 5.2, RFC 7591
/// section 3.2.2).
/// </summary>
internal static class McpOAuthJson
{
    /// <summary>The path of the RFC 8414 metadata document.</summary>
    public const string MetadataPath = "/.well-known/oauth-authorization-server";

    /// <summary>The authorization endpoint's path.</summary>
    public const string AuthorizePath = "/authorize";

    /// <summary>The token endpoint's path.</summary>
    public const string TokenPath = "/token";

    /// <summary>The registration endpoint's path.</summary>
    public const string RegisterPath = "/register";

    /// <summary>The <c>token_endpoint_auth_method</c> every registered client gets: public, with no secret.</summary>
    public const string PublicClientAuthMethod = "none";

    /// <summary>Writes the RFC 8414 metadata document.</summary>
    /// <param name="issuer">The issuer, <c>&lt;scheme&gt;://&lt;authority&gt;</c>.</param>
    /// <returns>The UTF-8 JSON.</returns>
    public static byte[] WriteMetadata(string issuer) => Write(writer =>
    {
        writer.WriteString("issuer", issuer);
        writer.WriteString("authorization_endpoint", issuer + AuthorizePath);
        writer.WriteString("token_endpoint", issuer + TokenPath);
        writer.WriteString("registration_endpoint", issuer + RegisterPath);
        WriteArray(writer, "response_types_supported", "code");
        WriteArray(writer, "grant_types_supported", "authorization_code", "refresh_token", "client_credentials");
        WriteArray(writer, "token_endpoint_auth_methods_supported", PublicClientAuthMethod, "client_secret_basic", "client_secret_post");
        WriteArray(writer, "code_challenge_methods_supported", McpPkce.Method);
    });

    /// <summary>Writes the answer to a registration (RFC 7591 section 3.2.1).</summary>
    /// <param name="clientId">The <c>client_id</c> issued.</param>
    /// <param name="client">The client registered.</param>
    /// <returns>The UTF-8 JSON.</returns>
    public static byte[] WriteRegistration(string clientId, McpOAuthClient client) => Write(writer =>
    {
        writer.WriteString("client_id", clientId);
        writer.WriteNumber("client_id_issued_at", client.IssuedAt.ToUnixTimeSeconds());
        WriteArray(writer, "redirect_uris", [.. client.RedirectUris]);
        writer.WriteString("token_endpoint_auth_method", PublicClientAuthMethod);
        WriteArray(writer, "grant_types", "authorization_code", "refresh_token");
        WriteArray(writer, "response_types", "code");
        if (client.ClientName is not null)
        {
            writer.WriteString("client_name", client.ClientName);
        }
    });

    /// <summary>Writes a successful token response (RFC 6749 section 5.1).</summary>
    /// <param name="tokens">The tokens issued.</param>
    /// <returns>The UTF-8 JSON.</returns>
    public static byte[] WriteTokens(McpTokenPair tokens) => Write(writer =>
    {
        writer.WriteString("access_token", tokens.AccessToken);
        writer.WriteString("token_type", "Bearer");
        writer.WriteNumber("expires_in", (long)McpAuthorizationGrants.AccessTokenLifetime.TotalSeconds);
        if (tokens.RefreshToken is not null)
        {
            writer.WriteString("refresh_token", tokens.RefreshToken);
        }
    });

    /// <summary>Writes an error object.</summary>
    /// <param name="error">The <c>error</c> code, such as <c>invalid_grant</c>.</param>
    /// <returns>The UTF-8 JSON.</returns>
    public static byte[] WriteError(string error) => Write(writer => writer.WriteString("error", error));

    private static void WriteArray(Utf8JsonWriter writer, string name, params string[] values)
    {
        writer.WriteStartArray(name);
        foreach (string value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static byte[] Write(Action<Utf8JsonWriter> writeProperties)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writeProperties(writer);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }
}
