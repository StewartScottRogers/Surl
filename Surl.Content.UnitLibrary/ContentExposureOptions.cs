namespace Surl.Content;

/// <summary>
/// What a content store exposes to every protocol server that reads it, and the largest upload
/// it accepts: ADR-0006 sections 1 and 2. A new instance holds ADR-0006's defaults.
/// </summary>
/// <remarks>
/// "Answered as absent" means the store returns exactly what it returns for a path that does
/// not exist, so no protocol server can answer a hidden entry differently from a missing one.
/// A symbolic link whose final target resolves outside the served root is refused whatever
/// these options say.
/// </remarks>
public sealed record ContentExposureOptions
{
    /// <summary>
    /// ADR-0006's maximum upload: 104857600 bytes (100 MiB).
    /// </summary>
    public const long DefaultMaxUploadBytes = 104857600;

    /// <summary>
    /// Every entry inside the served root served and listed, dot-files and symbolic links
    /// included, and uploads refused: what the store did before it took these options. No
    /// production code serves with it; <see cref="ContentStore(string, IContentFileSystem)"/>
    /// applies it for the HTTP, Gopher, DICT and TFTP tests that build a store without naming
    /// options, until BL-069 has each of them pass its own.
    /// </summary>
    public static ContentExposureOptions ServeEverythingInsideTheRoot { get; } = new()
    {
        ListDirectories = true,
        FollowSymbolicLinks = true,
        ServeDotFiles = true,
    };

    /// <summary>
    /// Whether uploads are written to the store (<c>--allow-uploads</c>); when not, every
    /// upload is <see cref="ContentUploadResult.NotPermitted"/>. Default: <see langword="false"/>.
    /// </summary>
    public bool AllowUploads { get; init; }

    /// <summary>
    /// Whether directories are listed (<c>--list-directories</c>); when not, a listing is
    /// answered as absent. Default: <see langword="false"/>.
    /// </summary>
    public bool ListDirectories { get; init; }

    /// <summary>
    /// Whether a symbolic link, junction or other reparse point inside the served root is
    /// followed when its final target also resolves inside it (<c>--follow-symlinks</c>); when
    /// not, a path through one is answered as absent and a listing leaves it out.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool FollowSymbolicLinks { get; init; }

    /// <summary>
    /// Whether a path with a segment that starts with <c>.</c> is served
    /// (<c>--serve-dot-files</c>); when not, it is answered as absent and a listing leaves it
    /// out. Default: <see langword="false"/>.
    /// </summary>
    public bool ServeDotFiles { get; init; }

    /// <summary>
    /// The most bytes one upload may hold (<c>--max-filesize</c>); 0 is no limit.
    /// Default: <see cref="DefaultMaxUploadBytes"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Set to a negative number.</exception>
    public long MaxUploadBytes
    {
        get;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The upload limit is a byte count, or 0 for no limit.");
    } = DefaultMaxUploadBytes;
}
