namespace Surl.Content;

/// <summary>
/// Names a file's media type from its extension, for every server that answers a file's
/// <c>Content-Type</c>: the HTTP server and the object-storage servers' defaults (ADR-0142).
/// </summary>
/// <remarks>
/// The extension is everything after the last <c>.</c> of the last path segment, compared without
/// regard to case. A name with no extension, a name whose only <c>.</c> is its first character
/// (<c>.profile</c>), and an extension not in the table are all named <see cref="Unknown"/>. No
/// <c>charset</c> parameter is added: the content store does not know a text file's encoding.
/// </remarks>
public static class ContentMediaTypes
{
    /// <summary>
    /// The media type of a file whose extension names none: <c>application/octet-stream</c>
    /// (RFC 9110, section 8.3).
    /// </summary>
    public const string Unknown = "application/octet-stream";

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["avif"] = "image/avif",
        ["bmp"] = "image/bmp",
        ["css"] = "text/css",
        ["csv"] = "text/csv",
        ["gif"] = "image/gif",
        ["gz"] = "application/gzip",
        ["htm"] = "text/html",
        ["html"] = "text/html",
        ["ico"] = "image/vnd.microsoft.icon",
        ["jpeg"] = "image/jpeg",
        ["jpg"] = "image/jpeg",
        ["js"] = "text/javascript",
        ["json"] = "application/json",
        ["md"] = "text/markdown",
        ["mjs"] = "text/javascript",
        ["mp3"] = "audio/mpeg",
        ["mp4"] = "video/mp4",
        ["oga"] = "audio/ogg",
        ["ogg"] = "audio/ogg",
        ["otf"] = "font/otf",
        ["pdf"] = "application/pdf",
        ["png"] = "image/png",
        ["svg"] = "image/svg+xml",
        ["tar"] = "application/x-tar",
        ["ttf"] = "font/ttf",
        ["txt"] = "text/plain",
        ["wasm"] = "application/wasm",
        ["wav"] = "audio/wav",
        ["webm"] = "video/webm",
        ["webp"] = "image/webp",
        ["woff"] = "font/woff",
        ["woff2"] = "font/woff2",
        ["xml"] = "application/xml",
        ["yaml"] = "application/yaml",
        ["yml"] = "application/yaml",
        ["zip"] = "application/zip",
    };

    /// <summary>
    /// Every extension the table names, without its <c>.</c>, with the media type it names.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Table => ByExtension;

    /// <summary>
    /// Names the media type of the file at <paramref name="path"/> from its extension.
    /// </summary>
    /// <param name="path">A file name or a path whose segments are joined by <c>/</c> or the
    /// platform's directory separator; only its last segment is read.</param>
    /// <returns>The media type the extension names, else <see cref="Unknown"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public static string Of(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var name = path[(path.LastIndexOfAny(['/', '\\', Path.DirectorySeparatorChar]) + 1)..];
        var dot = name.LastIndexOf('.');
        return dot > 0 && ByExtension.TryGetValue(name[(dot + 1)..], out var mediaType) ? mediaType : Unknown;
    }
}
