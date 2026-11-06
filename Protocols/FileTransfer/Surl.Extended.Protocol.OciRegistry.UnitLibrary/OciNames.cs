namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// The distribution specification's grammars for a repository name and a tag (Dist "Pulling
/// manifests"; ADR-0115, decision 1), checked by hand rather than by a generated regular
/// expression, so every branch is this library's own.
/// </summary>
internal static class OciNames
{
    /// <summary>
    /// The longest repository name served, in bytes.
    /// </summary>
    public const int MaxNameLength = 255;

    private const int MaxTagLength = 128;

    private const string TagCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-";

    /// <summary>
    /// Whether <paramref name="name"/> matches Dist's <c>&lt;name&gt;</c> grammar,
    /// <c>[a-z0-9]+((\.|_|__|-+)[a-z0-9]+)*(\/[a-z0-9]+((\.|_|__|-+)[a-z0-9]+)*)*</c>, and is at
    /// most <see cref="MaxNameLength"/> bytes long. The grammar admits ASCII only, so its length in
    /// characters is its length in bytes.
    /// </summary>
    /// <param name="name">The repository name, percent-decoded.</param>
    /// <returns><see langword="true"/> when the name is valid.</returns>
    public static bool IsRepositoryName(string name) =>
        name.Length <= MaxNameLength && name.Split('/').All(IsPathComponent);

    /// <summary>
    /// Whether <paramref name="reference"/> matches Dist's tag grammar
    /// <c>[a-zA-Z0-9_][a-zA-Z0-9._-]{0,127}</c>.
    /// </summary>
    /// <param name="reference">The manifest reference.</param>
    /// <returns><see langword="true"/> when the reference is a valid tag.</returns>
    public static bool IsTag(string reference) =>
        reference.Length is > 0 and <= MaxTagLength
        && (char.IsAsciiLetterOrDigit(reference[0]) || reference[0] == '_')
        && reference.AsSpan(1).IndexOfAnyExcept(TagCharacters) < 0;

    // [a-z0-9]+ runs joined by one separator each: ".", "_", "__" or one or more "-".
    private static bool IsPathComponent(string component)
    {
        var at = 0;
        while (true)
        {
            var run = LowerAlphanumericRun(component, at);
            if (run == 0)
            {
                return false;
            }

            at += run;
            if (at == component.Length)
            {
                return true;
            }

            var separator = SeparatorRun(component, at);
            if (!IsSeparator(separator))
            {
                return false;
            }

            at += separator.Length;
        }
    }

    private static int LowerAlphanumericRun(string text, int start)
    {
        var end = start;
        while (end < text.Length && (char.IsAsciiLetterLower(text[end]) || char.IsAsciiDigit(text[end])))
        {
            end++;
        }

        return end - start;
    }

    private static string SeparatorRun(string text, int start)
    {
        var end = start;
        while (end < text.Length && text[end] is '.' or '_' or '-')
        {
            end++;
        }

        return text[start..end];
    }

    private static bool IsSeparator(string separator) =>
        separator is "." or "_" or "__" || (separator.Length > 0 && separator.AsSpan().IndexOfAnyExcept('-') < 0);
}
