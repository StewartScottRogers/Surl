using System.Text;

namespace Surl.Protocol.Ftp;

/// <summary>
/// Resolves the paths an FTP client names against the session's current directory, and turns
/// a resolved path into a content-store request path and into reply text (ADR-0052, decision 2).
/// </summary>
/// <remarks>
/// A path is a list of segments from <c>/</c>: the empty list is <c>/</c> itself. A path is
/// read as UTF-8; one that is not valid UTF-8 has no resolution. A path starting with <c>/</c>
/// is absolute and any other is relative to the current directory; empty and <c>.</c> segments
/// are dropped and <c>..</c> removes the segment before it, lexically, and a <c>..</c> that
/// would climb above <c>/</c> leaves the path with no resolution.
/// </remarks>
internal static class FtpPath
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Resolves <paramref name="argument"/> against <paramref name="currentDirectory"/>.
    /// </summary>
    /// <param name="currentDirectory">The session's current directory.</param>
    /// <param name="argument">The path as the client sent it.</param>
    /// <returns>The resolved path, or <see langword="null"/> when the argument is not UTF-8 or climbs above <c>/</c>.</returns>
    public static IReadOnlyList<string>? Resolve(IReadOnlyList<string> currentDirectory, byte[] argument)
    {
        string text;
        try
        {
            text = StrictUtf8.GetString(argument);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        var segments = text.StartsWith('/') ? new List<string>() : new List<string>(currentDirectory);
        foreach (var segment in text.Split('/'))
        {
            if (!TryApplySegment(segments, segment))
            {
                return null;
            }
        }

        return segments;
    }

    /// <summary>
    /// The parent of <paramref name="directory"/>, or <c>/</c> itself for <c>/</c>.
    /// </summary>
    /// <param name="directory">A resolved directory.</param>
    /// <returns>Its parent.</returns>
    public static IReadOnlyList<string> Parent(IReadOnlyList<string> directory) =>
        directory.Count == 0 ? directory : [.. directory.Take(directory.Count - 1)];

    /// <summary>
    /// The request path <see cref="Surl.Content.ContentStore.MapRequestPath(string)"/> takes for
    /// <paramref name="path"/>: each segment percent-encoded, so no byte of a name is read as a
    /// separator or an escape.
    /// </summary>
    /// <param name="path">A resolved path.</param>
    /// <returns>The percent-encoded request path, starting with <c>/</c>.</returns>
    public static string ToRequestPath(IReadOnlyList<string> path) =>
        "/" + string.Join('/', path.Select(Uri.EscapeDataString));

    /// <summary>
    /// <paramref name="path"/> as it appears inside the quotes of a <c>257</c> reply: each UTF-8
    /// byte that is printable ASCII other than <c>\</c> as itself, with <c>"</c> doubled (RFC
    /// 959, appendix II), and every other byte as <c>\xHH</c> (ADR-0006, section 3).
    /// </summary>
    /// <param name="path">A resolved path.</param>
    /// <returns>The rendered path, starting with <c>/</c>.</returns>
    public static string ToQuotedReplyText(IReadOnlyList<string> path) =>
        Render(Encoding.UTF8.GetBytes("/" + string.Join('/', path)), doublesQuotes: true);

    /// <summary>
    /// A path as the client sent it, as a reply outside quotes echoes it: each byte that is
    /// printable ASCII other than <c>\</c> as itself, and every other byte as <c>\xHH</c>
    /// (ADR-0006, section 3).
    /// </summary>
    /// <param name="sentPath">The path's bytes as sent.</param>
    /// <returns>The rendered path.</returns>
    public static string ToReplyText(byte[] sentPath) => Render(sentPath, doublesQuotes: false);

    /// <summary>
    /// A resolved path as a reply outside quotes names it, as <see cref="ToReplyText(byte[])"/>
    /// renders its UTF-8 bytes.
    /// </summary>
    /// <param name="path">A resolved path.</param>
    /// <returns>The rendered path, starting with <c>/</c>.</returns>
    public static string ToReplyText(IReadOnlyList<string> path) =>
        Render(Encoding.UTF8.GetBytes("/" + string.Join('/', path)), doublesQuotes: false);

    private static string Render(byte[] bytes, bool doublesQuotes)
    {
        var rendered = new StringBuilder();
        foreach (var character in bytes)
        {
            rendered.Append(RenderByte(character, doublesQuotes));
        }

        return rendered.ToString();
    }

    private static string RenderByte(byte character, bool doublesQuotes) => character switch
    {
        (byte)'"' when doublesQuotes => "\"\"",
        (byte)'\\' => @"\x5C",
        >= 0x20 and <= 0x7E => ((char)character).ToString(),
        _ => $@"\x{character:X2}",
    };

    private static bool TryApplySegment(List<string> segments, string segment)
    {
        switch (segment)
        {
            case "" or ".":
                return true;
            case "..":
                if (segments.Count == 0)
                {
                    return false;
                }

                segments.RemoveAt(segments.Count - 1);
                return true;
            default:
                segments.Add(segment);
                return true;
        }
    }
}
