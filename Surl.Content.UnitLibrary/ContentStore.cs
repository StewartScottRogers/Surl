namespace Surl.Content;

/// <summary>
/// The content store: the served root, and the rules that map a request path onto a location
/// inside it without ever escaping it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MapRequestPath(string)"/> takes the path component of a request, still
/// percent-encoded and without its query. It must start with <c>/</c>; it is split on
/// <c>/</c>, and each segment is percent-decoded as UTF-8 and checked on its own, so an
/// encoded <c>%2F</c> or <c>%5C</c> can never become a separator. One trailing <c>/</c> is
/// allowed; any other empty segment is refused, which refuses <c>//server/share</c>.
/// </para>
/// <para>
/// Every dot segment is refused, including one that would stay inside the root such as
/// <c>/a/../b</c>. Upstream curl removes dot segments before it sends a request, so a raw or
/// percent-encoded <c>..</c> reaches Surl only when the client asked for it on purpose
/// (<c>--path-as-is</c>, or an encoding curl does not normalise); refusing it outright keeps
/// the rule one line long and leaves nothing to get wrong about where a <c>..</c> lands.
/// </para>
/// <para>
/// A decoded segment is also refused when it holds <c>/</c>, <c>\</c>, <c>:</c> (a drive
/// letter or an alternate data stream) or a control character, ends in <c>.</c> or a space
/// (which Windows strips), or is a Windows device name such as <c>CON</c> or <c>NUL</c>. These
/// rules apply on every platform, so a request is answered the same way wherever Surl runs.
/// </para>
/// <para>
/// A well-formed path is joined to the served root and resolved through
/// <see cref="IContentFileSystem.ResolveFinalPath(string)"/>; if a symbolic link along it
/// finally lands outside the root, itself resolved the same way, the path is refused. The
/// comparison is ordinal, so the seam must spell the root the same way in every path it
/// returns; a different spelling is refused, never let through. The content store never opens anything: the seam is asked only
/// where a path resolves and what is there.
/// </para>
/// </remarks>
public sealed class ContentStore
{
    private readonly IContentFileSystem fileSystem;

    /// <summary>
    /// Creates a content store that serves <paramref name="servedRoot"/>.
    /// </summary>
    /// <param name="servedRoot">The full path of the directory being served.</param>
    /// <param name="fileSystem">The seam every look at the served root goes through.</param>
    public ContentStore(string servedRoot, IContentFileSystem fileSystem)
    {
        ArgumentException.ThrowIfNullOrEmpty(servedRoot);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ServedRoot = servedRoot;
        this.fileSystem = fileSystem;
    }

    /// <summary>
    /// The full path of the directory being served.
    /// </summary>
    public string ServedRoot { get; }

    /// <summary>
    /// Maps a request path onto a location inside the served root, or refuses it.
    /// </summary>
    /// <param name="requestPath">The path component of the request, still percent-encoded.</param>
    /// <returns>The resolved location inside the root and what is there, or the refusal and
    /// its reason. A refusal is a result, never an exception.</returns>
    public ContentPathMapping MapRequestPath(string requestPath)
    {
        ArgumentNullException.ThrowIfNull(requestPath);
        ContentPathRefusal refusal = RequestPathSegments.Split(requestPath, out string[] segments);
        if (refusal != ContentPathRefusal.None)
        {
            return ContentPathMapping.Refused(refusal);
        }

        string resolvedRoot = fileSystem.ResolveFinalPath(ServedRoot);
        string resolved = fileSystem.ResolveFinalPath(Path.Join([ServedRoot, .. segments]));
        if (!IsInsideOrAt(resolved, resolvedRoot))
        {
            return ContentPathMapping.Refused(ContentPathRefusal.ResolvesOutsideRoot);
        }

        return ContentPathMapping.Mapped(resolved, fileSystem.GetEntryKind(resolved));
    }

    private static bool IsInsideOrAt(string path, string root)
    {
        string trimmedRoot = Path.TrimEndingDirectorySeparator(root);
        if (string.Equals(Path.TrimEndingDirectorySeparator(path), trimmedRoot, StringComparison.Ordinal))
        {
            return true;
        }

        string prefix = Path.EndsInDirectorySeparator(trimmedRoot)
            ? trimmedRoot
            : trimmedRoot + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.Ordinal);
    }
}
