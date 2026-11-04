using Surl.Content;

namespace Surl.Protocol.Ldap;

/// <summary>
/// The file the LDAP server's directory is read from, through <see cref="IContentFileSystem"/>
/// (ADR-0072 decision 1): <see cref="FileName"/> in the state folder it is given,
/// <c>&lt;data directory&gt;/.surl/ldap</c> when surl serves with <c>--directory</c>.
/// </summary>
/// <remarks>
/// It is read once, at start, and never written: the directory is read-only while serving. A
/// missing file is an empty directory.
/// </remarks>
public sealed class LdapDirectoryFile
{
    /// <summary>
    /// The file's name in its state folder: <c>directory.ldif</c>.
    /// </summary>
    public const string FileName = "directory.ldif";

    private readonly IContentFileSystem fileSystem;

    /// <summary>
    /// Creates the directory's file in <paramref name="stateFolderPath"/>, read through
    /// <paramref name="fileSystem"/>. Nothing is read until the directory is loaded.
    /// </summary>
    /// <param name="fileSystem">The seam the file is read through.</param>
    /// <param name="stateFolderPath">The full path of the folder the file is kept in.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="stateFolderPath"/> is empty.</exception>
    public LdapDirectoryFile(IContentFileSystem fileSystem, string stateFolderPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrEmpty(stateFolderPath);

        this.fileSystem = fileSystem;
        FilePath = Path.Join(stateFolderPath, FileName);
    }

    /// <summary>
    /// The full path of the file: <see cref="FileName"/> in the state folder.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Reads the file into a directory; an empty one when there is no file.
    /// </summary>
    /// <param name="timeProvider">The clock a search's <c>timeLimit</c> is measured by.</param>
    /// <param name="maxEntries">The most entries the directory holds.</param>
    /// <param name="maxTotalBytes">The most bytes of DNs, descriptions and values the directory holds.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The directory.</returns>
    /// <exception cref="LdapDirectoryLoadException">The file cannot be read, or cannot be the directory.</exception>
    internal async Task<LdapDirectory> LoadAsync(
        TimeProvider timeProvider,
        int maxEntries = LdapDirectory.DefaultMaxEntries,
        long maxTotalBytes = LdapDirectory.DefaultMaxTotalBytes,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAsync(cancellationToken);
        try
        {
            return DirectoryOf(LdifReader.Read(bytes), timeProvider, maxEntries, maxTotalBytes);
        }
        catch (LdifFormatException exception)
        {
            throw new LdapDirectoryLoadException(FilePath, exception.Message, exception);
        }
    }

    private static LdapDirectory DirectoryOf(IReadOnlyList<LdifRecord> records, TimeProvider timeProvider, int maxEntries, long maxTotalBytes)
    {
        try
        {
            return new LdapDirectory(records.Select(record => record.Entry).ToArray(), timeProvider, maxEntries, maxTotalBytes);
        }
        catch (LdapDirectoryException exception)
        {
            throw new LdifFormatException(records[exception.EntryIndex].Line, LdifFaultText.Of(exception.Fault));
        }
    }

    /// <summary>
    /// Reads the file's bytes; none when there is no file, which reads as an empty directory.
    /// </summary>
    private async Task<byte[]> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (fileSystem.GetEntryKind(FilePath) != ContentEntryKind.File)
            {
                return [];
            }

            var bytes = new MemoryStream();
            await using (var stream = fileSystem.OpenFileForAsyncRead(FilePath))
            {
                await stream.CopyToAsync(bytes, cancellationToken);
            }

            return bytes.ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new LdapDirectoryLoadException(FilePath, exception.Message, exception);
        }
    }
}
