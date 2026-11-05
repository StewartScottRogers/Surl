namespace Surl.Content;

/// <summary>
/// A named share: a top-level folder of the content store whose files live at
/// <see cref="Path"/>, through its own file-system seam (<c>--share</c>, ADR-0102 decision 2.1).
/// </summary>
/// <remarks>
/// The share's name is the first segment of every request path that reaches it, and shadows a
/// root entry of the same name, compared ordinally, for every scheme. Inside the share every rule
/// of the served root holds: the exposure options, the segment refusals, no escape through a
/// symbolic link, and <c>.surl</c> as the share's first segment never served.
/// </remarks>
public sealed class ContentShare
{
    /// <summary>
    /// Creates a share named <paramref name="name"/> whose files live at <paramref name="path"/>.
    /// </summary>
    /// <param name="name">One path segment a request path may hold, and not <c>.surl</c>.</param>
    /// <param name="path">The full path of the share's directory.</param>
    /// <param name="fileSystem">The seam every look inside the share goes through.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid folder name, or
    /// <paramref name="path"/> is empty or relative to the current directory.</exception>
    public ContentShare(string name, string path, IContentFileSystem fileSystem)
    {
        ContentStore.ThrowUnlessTopLevelFolderName(name, nameof(name));
        ContentStore.ThrowUnlessIndependentOfTheCurrentDirectory(path, nameof(path));
        ArgumentNullException.ThrowIfNull(fileSystem);
        Name = name;
        Path = path;
        FileSystem = fileSystem;
    }

    /// <summary>The share's name: the first segment of a request path that reaches it.</summary>
    public string Name { get; }

    /// <summary>The full path of the share's directory, kept as given.</summary>
    public string Path { get; }

    /// <summary>The seam every look inside the share goes through.</summary>
    public IContentFileSystem FileSystem { get; }

    /// <summary>
    /// Whether every write in the share - upload, append, rename, delete, folder create or remove,
    /// last write time - is refused as not permitted, whatever
    /// <see cref="ContentExposureOptions.AllowUploads"/> says (<c>readonly</c>). Default:
    /// <see langword="false"/>.
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// The schemes that see the share, compared ignoring case (<c>protocols=</c>); to any other
    /// scheme, and to a lookup that names no scheme, the share is missing.
    /// <see langword="null"/>, the default, is every scheme.
    /// </summary>
    public IReadOnlySet<string>? Schemes
    {
        get;
        init => field = value is null ? null : new HashSet<string>(value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The member that grants every caller, logged in or anonymous (ADR-0102 decision 4.2).
    /// </summary>
    public const string EveryoneMember = "@everyone";

    /// <summary>
    /// Who may read and who may write in the share (<c>--share-access</c>), by account name,
    /// compared ordinally, and <see cref="EveryoneMember"/>; groups arrive already expanded.
    /// <see langword="null"/>, the default, is a share no access row names: open to every caller
    /// as the root is. A share with grants, even none, is closed to every account not granted and
    /// to every anonymous caller not reached through <see cref="EveryoneMember"/>.
    /// </summary>
    public IReadOnlyDictionary<string, ContentShareAccess>? Grants
    {
        get;
        init => field = value is null ? null : new Dictionary<string, ContentShareAccess>(value, StringComparer.Ordinal);
    }

    /// <summary>
    /// What <paramref name="caller"/> may do in the share by its grants: the wider of its own
    /// account's grant and <see cref="EveryoneMember"/>'s.
    /// </summary>
    /// <param name="caller">The caller asking.</param>
    /// <returns>The access granted; <see langword="null"/> when the caller may not even read.</returns>
    public ContentShareAccess? AccessFor(ContentCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (Grants is null)
        {
            return ContentShareAccess.ReadWrite;
        }

        ContentShareAccess? everyone = Grants.TryGetValue(EveryoneMember, out ContentShareAccess shared) ? shared : null;
        ContentShareAccess? own = caller.AccountName is not null && Grants.TryGetValue(caller.AccountName, out ContentShareAccess granted) ? granted : null;
        return own > everyone || everyone is null ? own : everyone;
    }

    /// <summary>
    /// Whether a lookup made for <paramref name="scheme"/> sees the share.
    /// </summary>
    /// <param name="scheme">The calling server's scheme; <see langword="null"/> for a lookup that
    /// names none, which sees only a share open to every scheme.</param>
    /// <returns>Whether the share is visible.</returns>
    public bool IsVisibleTo(string? scheme) =>
        Schemes is null || (scheme is not null && Schemes.Contains(scheme));
}
