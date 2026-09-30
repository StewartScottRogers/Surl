using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// An SFTP path as ADR-0054 decision 1 reads it: UTF-8, resolved against the home directory
/// <c>/</c>, with repeated <c>/</c> collapsed, <c>.</c> segments dropped and each <c>..</c>
/// removing the segment before it, stopping at <c>/</c>; a trailing <c>/</c> is kept. The
/// canonical path is then percent-encoded segment by segment for the content store, so every
/// exposure rule applies to it exactly as over HTTP and FTP.
/// </summary>
internal static class SftpPath
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The canonical form of the path a client sent.
    /// </summary>
    /// <param name="path">The path's bytes as the client sent them.</param>
    /// <returns>
    /// The canonical path, starting <c>/</c>, ending <c>/</c> only when the client's did and it is
    /// not the root; <see langword="null"/> when the bytes are not UTF-8 or hold a NUL, which is
    /// answered as absent. The empty path is the root.
    /// </returns>
    public static string? Canonicalise(ReadOnlySpan<byte> path)
    {
        string text;
        try
        {
            text = StrictUtf8.GetString(path);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        if (text.Contains('\0'))
        {
            return null;
        }

        var segments = ResolveSegments(text);
        var canonical = "/" + string.Join('/', segments);

        return text.EndsWith('/') && segments.Count > 0 ? canonical + "/" : canonical;
    }

    /// <summary>
    /// The canonical path without its trailing <c>/</c>, as <c>REALPATH</c> names it.
    /// </summary>
    /// <param name="canonical">A path <see cref="Canonicalise"/> returned.</param>
    /// <returns>The path; <c>/</c> for the root.</returns>
    public static string WithoutTrailingSlash(string canonical) =>
        canonical.Length > 1 ? canonical.TrimEnd('/') : canonical;

    /// <summary>
    /// The request path the content store maps: each segment percent-encoded, every byte outside
    /// <c>A-Z a-z 0-9 - . _ ~</c> as <c>%HH</c>, so a name holding <c>%</c> means itself.
    /// </summary>
    /// <param name="canonical">A path <see cref="Canonicalise"/> returned.</param>
    /// <returns>The percent-encoded request path.</returns>
    public static string ToRequestPath(string canonical) =>
        string.Join('/', canonical.Split('/').Select(Uri.EscapeDataString));

    private static List<string> ResolveSegments(string text)
    {
        var segments = new List<string>();
        foreach (var segment in text.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(segment => segment != "."))
        {
            if (segment == "..")
            {
                RemoveLast(segments);
            }
            else
            {
                segments.Add(segment);
            }
        }

        return segments;
    }

    // A climb above the root stops there.
    private static void RemoveLast(List<string> segments)
    {
        if (segments.Count > 0)
        {
            segments.RemoveAt(segments.Count - 1);
        }
    }
}
