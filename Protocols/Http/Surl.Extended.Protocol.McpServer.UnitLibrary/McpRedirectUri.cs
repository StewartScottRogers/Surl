using System.Text;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The redirect URIs the authorization server accepts and how it sends a client back to one (ADR-0111 decision
/// 6.3; OAuth 2.1 section 2.3): an absolute <c>https</c> URI, or an <c>http</c> URI whose host is loopback, with no
/// fragment.
/// </summary>
internal static class McpRedirectUri
{
    /// <summary>Whether a client may register the URI.</summary>
    /// <param name="uri">The URI as the client gave it.</param>
    /// <returns><see langword="true" /> for an <c>https</c> URI or a loopback <c>http</c> one, without a fragment.</returns>
    public static bool IsAllowed(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed) || parsed.Fragment.Length > 0 || uri.Contains('#', StringComparison.Ordinal))
        {
            return false;
        }

        return parsed.Scheme == Uri.UriSchemeHttps || (parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback);
    }

    /// <summary>The redirect URI with parameters added to its query.</summary>
    /// <param name="uri">The registered redirect URI.</param>
    /// <param name="parameters">Each name and value; a <see langword="null" /> value is left out.</param>
    /// <returns>The URI to send the client to.</returns>
    public static string WithParameters(string uri, params (string Name, string? Value)[] parameters)
    {
        StringBuilder result = new(uri);
        char separator = uri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        foreach ((string name, string? value) in parameters.Where(parameter => parameter.Value is not null))
        {
            result.Append(separator).Append(name).Append('=').Append(Uri.EscapeDataString(value!));
            separator = '&';
        }

        return result.ToString();
    }
}
