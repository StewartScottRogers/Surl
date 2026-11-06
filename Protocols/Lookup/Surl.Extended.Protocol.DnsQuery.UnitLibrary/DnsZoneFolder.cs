using Surl.Content;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The folder the DNS server's zones are read from, through <see cref="IContentFileSystem"/>
/// (ADR-0092 decision 1): every <c>*.zone</c> file in it, <c>&lt;data directory&gt;/.surl/dns</c>
/// when surl serves with <c>--directory</c>. The files are read once, at start, in ordinal order
/// of their names, and never written. A missing folder is no zone at all.
/// </summary>
public sealed class DnsZoneFolder
{
    private readonly IContentFileSystem fileSystem;

    /// <summary>Creates the folder at <paramref name="folderPath"/>, read through <paramref name="fileSystem"/>. Nothing is read until it is loaded.</summary>
    /// <param name="fileSystem">The seam the files are read through.</param>
    /// <param name="folderPath">The full path of the folder.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="folderPath"/> is empty.</exception>
    public DnsZoneFolder(IContentFileSystem fileSystem, string folderPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrEmpty(folderPath);

        this.fileSystem = fileSystem;
        FolderPath = folderPath;
    }

    /// <summary>The full path of the folder.</summary>
    public string FolderPath { get; }

    /// <summary>Reads every zone file into <paramref name="zones"/>, an empty set when there is no folder.</summary>
    /// <param name="zones">The set the zones are added to, with its bounds.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns><paramref name="zones"/>, holding every zone.</returns>
    /// <exception cref="DnsZoneLoadException">A file cannot be read, or cannot be a zone.</exception>
    public async Task<DnsZoneSet> LoadAsync(DnsZoneSet zones, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zones);

        foreach (var fileName in ZoneFileNames())
        {
            var filePath = Path.Join(FolderPath, fileName);

            try
            {
                zones.Add(fileName, await ReadAsync(filePath, cancellationToken));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DnsZoneFileException)
            {
                throw new DnsZoneLoadException(filePath, exception.Message, exception);
            }
        }

        return zones;
    }

    private IEnumerable<string> ZoneFileNames() =>
        fileSystem.GetEntryKind(FolderPath) != ContentEntryKind.Directory
            ? []
            : fileSystem.EnumerateDirectoryEntryNames(FolderPath)
                .Where(name => name.EndsWith(DnsZoneSet.ZoneFileExtension, StringComparison.OrdinalIgnoreCase)
                    && fileSystem.GetEntryKind(Path.Join(FolderPath, name)) == ContentEntryKind.File)
                .Order(StringComparer.Ordinal)
                .ToArray();

    private async Task<byte[]> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        var bytes = new MemoryStream();
        await using (var stream = fileSystem.OpenFileForAsyncRead(filePath))
        {
            await stream.CopyToAsync(bytes, cancellationToken);
        }

        return bytes.ToArray();
    }
}
