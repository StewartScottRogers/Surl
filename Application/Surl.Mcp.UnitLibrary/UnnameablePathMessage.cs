namespace Surl.Mcp;

/// <summary>
/// The tool-error text for a path the content store refuses to name (BL-1396). It is built from why the path is
/// refused, never from the exception's own message, which ends in .NET's <c>(Parameter 'requestPath')</c>.
/// </summary>
internal static class UnnameablePathMessage
{
    /// <summary>Says what is wrong with <paramref name="path"/> and quotes it as the agent gave it.</summary>
    /// <param name="path">The path the agent gave.</param>
    /// <returns>The text of the tool error.</returns>
    internal static string For(string path)
    {
        if (!path.StartsWith('/'))
        {
            return $"path {path} must start with /";
        }

        if (path.Split('/').Any(segment => segment is "." or ".."))
        {
            return $"path {path} may not hold a . or .. segment";
        }

        return $"path {path} is not one a request may name";
    }
}
