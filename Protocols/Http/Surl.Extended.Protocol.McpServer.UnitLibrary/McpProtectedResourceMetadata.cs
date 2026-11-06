using System.Buffers;
using System.Text.Json;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The OAuth 2.0 protected resource metadata (RFC 9728) of the MCP endpoint, and the <c>WWW-Authenticate</c>
/// challenges that point to it (ADR-0111 decisions 6.1 and 6.2). The server is its own authorization server,
/// at the issuer <c>&lt;scheme&gt;://&lt;authority&gt;</c>.
/// </summary>
internal static class McpProtectedResourceMetadata
{
    /// <summary>The well-known segment inserted before the endpoint's path (RFC 9728 section 3.1).</summary>
    public const string WellKnownPrefix = "/.well-known/oauth-protected-resource";

    /// <summary>The <c>resource_name</c> the document carries.</summary>
    public const string ResourceName = "Surl MCP server";

    /// <summary>The metadata document's path for an endpoint.</summary>
    /// <param name="endpointPath">The MCP endpoint's path, such as <c>/mcp</c>.</param>
    /// <returns>The path, such as <c>/.well-known/oauth-protected-resource/mcp</c>.</returns>
    public static string PathFor(string endpointPath) => WellKnownPrefix + endpointPath;

    /// <summary>The challenge a <c>401</c> carries (RFC 9728 section 5.1; RFC 6750 section 3.1).</summary>
    /// <param name="issuer">The <c>&lt;scheme&gt;://&lt;authority&gt;</c> the request reached.</param>
    /// <param name="endpointPath">The MCP endpoint's path.</param>
    /// <param name="hadToken">Whether the request carried a token, which was found invalid.</param>
    /// <returns>The <c>WWW-Authenticate</c> value.</returns>
    public static string Challenge(string issuer, string endpointPath, bool hadToken) =>
        $"Bearer resource_metadata=\"{issuer}{PathFor(endpointPath)}\"" + (hadToken ? ", error=\"invalid_token\"" : string.Empty);

    /// <summary>Writes the metadata document.</summary>
    /// <param name="issuer">The <c>&lt;scheme&gt;://&lt;authority&gt;</c> the request reached.</param>
    /// <param name="endpointPath">The MCP endpoint's path.</param>
    /// <returns>The document's UTF-8 JSON.</returns>
    public static byte[] Write(string issuer, string endpointPath)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("resource", issuer + endpointPath);
            writer.WriteStartArray("authorization_servers");
            writer.WriteStringValue(issuer);
            writer.WriteEndArray();
            writer.WriteStartArray("bearer_methods_supported");
            writer.WriteStringValue("header");
            writer.WriteEndArray();
            writer.WriteString("resource_name", ResourceName);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }
}
