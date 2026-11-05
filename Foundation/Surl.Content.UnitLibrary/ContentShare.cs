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
    /// Whether a lookup made for <paramref name="scheme"/> sees the share.
    /// </summary>
    /// <param name="scheme">The calling server's scheme; <see langword="null"/> for a lookup that
    /// names none, which sees only a share open to every scheme.</param>
    /// <returns>Whether the share is visible.</returns>
    public bool IsVisibleTo(string? scheme) =>
        Schemes is null || (scheme is not null && Schemes.Contains(scheme));
}
