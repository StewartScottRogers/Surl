using System.Text.Json;
using Surl.Content;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Turns a tool's <c>path</c> argument - <c>/</c>-separated, relative to the content store's root, a leading
/// <c>/</c> ignored (ADR-0111 decision 2) - into a content store mapping for the caller.
/// </summary>
internal static class McpToolPath
{
    /// <summary>Reads the <c>path</c> argument without its leading <c>/</c>; empty when it is absent.</summary>
    /// <param name="arguments">The tool's checked arguments.</param>
    /// <returns>The path relative to the root.</returns>
    internal static string ReadRelativePath(JsonElement arguments)
    {
        string path = arguments.TryGetProperty("path", out JsonElement given) ? given.GetString()! : string.Empty;
        return path.StartsWith('/') ? path[1..] : path;
    }

    /// <summary>
    /// Percent-encodes each segment of a relative path into the request path the content store maps, so the store
    /// sees exactly the characters given and applies its own refusals to them.
    /// </summary>
    /// <param name="relativePath">The path relative to the root.</param>
    /// <returns>The request path, starting with <c>/</c>.</returns>
    internal static string ToRequestPath(string relativePath) =>
        "/" + string.Join('/', relativePath.Split('/').Select(Uri.EscapeDataString));

    /// <summary>Maps a relative path for a caller.</summary>
    /// <param name="store">The content store.</param>
    /// <param name="caller">Who asks.</param>
    /// <param name="relativePath">The path relative to the root.</param>
    /// <returns>The mapping, refused or not.</returns>
    internal static ContentPathMapping Map(ContentStore store, ContentCaller caller, string relativePath) =>
        store.MapRequestPathForCaller(ToRequestPath(relativePath), caller);
}
