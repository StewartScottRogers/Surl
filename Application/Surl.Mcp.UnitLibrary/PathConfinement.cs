namespace Surl.Mcp;

/// <summary>
/// Decides whether a local path an option names lies inside the host's root (ADR-0103 decision 4): the path is
/// taken as the server will open it - relative to the process's current directory, which the host sets to the
/// root - and it and the root are both resolved component by component, following every symbolic link and
/// applying <c>..</c> to the resolved directory, before the one is checked to lie in the other. Names are
/// compared ordinally, so a path spelled in another case than the root is refused rather than guessed at.
/// </summary>
internal sealed class PathConfinement
{
    /// <summary>The most links followed while resolving one path; past it the links are taken to loop.</summary>
    internal const int MaxLinkHops = 40;

    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    private readonly string? root;
    private readonly Func<string> readCurrentDirectory;
    private readonly Func<string, string?> readLinkTarget;

    /// <summary>Confines paths to <paramref name="root"/> as the file system on disk resolves them.</summary>
    /// <param name="root">The root, or <see langword="null"/> for none, which refuses every path.</param>
    public PathConfinement(string? root)
        : this(root, () => Environment.CurrentDirectory, ReadLinkTargetOnDisk)
    {
    }

    /// <summary>Confines paths to <paramref name="root"/> through the given seams.</summary>
    /// <param name="root">The root, or <see langword="null"/> for none, which refuses every path.</param>
    /// <param name="readCurrentDirectory">Reads the directory a relative path is taken from.</param>
    /// <param name="readLinkTarget">Reads a symbolic link's target as stored, or <see langword="null"/> when the path is not a link.</param>
    internal PathConfinement(string? root, Func<string> readCurrentDirectory, Func<string, string?> readLinkTarget)
    {
        this.root = root;
        this.readCurrentDirectory = readCurrentDirectory;
        this.readLinkTarget = readLinkTarget;
    }

    /// <summary>Says why <paramref name="path"/>, named by <paramref name="optionName"/>, is refused, or returns <see langword="null"/> when it lies inside the root.</summary>
    /// <param name="optionName">The option that names the path, as the refusal names it (<c>--cert</c>).</param>
    /// <param name="path">The path as given.</param>
    /// <returns>The rule the path breaks, or <see langword="null"/>.</returns>
    public string? FindRefusal(string optionName, string path)
    {
        if (root is null)
        {
            return $"{optionName} names a local path, and this host was started with no root (--root or SURL_MCP_ROOT)";
        }

        var resolvedRoot = Resolve(Path.GetFullPath(root));
        var resolvedPath = Resolve(Path.IsPathFullyQualified(path) ? path : Path.Combine(readCurrentDirectory(), path));
        return resolvedRoot is not null && resolvedPath is not null && IsInside(resolvedPath, resolvedRoot)
            ? null
            : $"{optionName} names {path}, which is not inside the root";
    }

    /// <summary>Reads the target of the link at <paramref name="path"/> on disk, or <see langword="null"/> when it is not one.</summary>
    /// <param name="path">The path to read.</param>
    /// <returns>The link's target as stored, or <see langword="null"/>.</returns>
    internal static string? ReadLinkTargetOnDisk(string path) =>
        Directory.Exists(path) ? new DirectoryInfo(path).LinkTarget : new FileInfo(path).LinkTarget;

    private static bool IsInside(string resolvedPath, string resolvedRoot) =>
        resolvedPath == resolvedRoot
        || resolvedPath.StartsWith(
            Path.EndsInDirectorySeparator(resolvedRoot) ? resolvedRoot : resolvedRoot + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);


    /// <summary>Pushes <paramref name="path"/>'s components onto <paramref name="remaining"/>, first on top, and returns its root, the place resolving it starts.</summary>
    private static string StartResolving(Stack<string> remaining, string path)
    {
        var pathRoot = Path.GetPathRoot(path)!;
        var components = path[pathRoot.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        for (var index = components.Length - 1; index >= 0; index--)
        {
            remaining.Push(components[index]);
        }

        return Path.GetFullPath(pathRoot);
    }

    /// <summary>Applies <c>.</c> or <c>..</c> to the resolved directory <paramref name="current"/>; <c>..</c> of a file system root is that root.</summary>
    private static string ApplyDots(string current, string component) =>
        component == "." ? current : Path.GetDirectoryName(current) ?? current;

    /// <summary>
    /// Resolves <paramref name="path"/>, which is rooted, to the path it names with no link in it; null when its
    /// links loop.
    /// </summary>
    private string? Resolve(string path)
    {
        var remaining = new Stack<string>();
        string? current = StartResolving(remaining, path);
        var hops = 0;
        while (current is not null && remaining.TryPop(out var component))
        {
            current = component is "." or ".."
                ? ApplyDots(current, component)
                : Descend(current, component, remaining, ref hops);
        }

        return current;
    }

    /// <summary>
    /// Steps from <paramref name="current"/> into <paramref name="component"/>; when that is a link, starts resolving
    /// its target instead. Null once more than <see cref="MaxLinkHops"/> links were followed.
    /// </summary>
    private string? Descend(string current, string component, Stack<string> remaining, ref int hops)
    {
        var next = Path.Combine(current, component);
        var target = readLinkTarget(next);
        if (target is null)
        {
            return next;
        }

        return ++hops > MaxLinkHops
            ? null
            : StartResolving(remaining, Path.IsPathFullyQualified(target) ? target : Path.Combine(current, target));
    }
}
