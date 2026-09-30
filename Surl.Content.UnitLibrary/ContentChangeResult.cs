namespace Surl.Content;

/// <summary>
/// What became of a change to the served root's entries that <see cref="ContentStore"/> was
/// asked to make: <see cref="ContentStore.DeleteFile(ContentPathMapping)"/>,
/// <see cref="ContentStore.RenameEntry(ContentPathMapping, ContentPathMapping)"/>,
/// <see cref="ContentStore.CreateDirectory(ContentPathMapping)"/> or
/// <see cref="ContentStore.RemoveEmptyDirectory(ContentPathMapping)"/>.
/// </summary>
/// <remarks>
/// Each member but <see cref="Done"/> changes nothing. The FTP server answers each with the
/// reply ADR-0052 decision 8 gives the command.
/// </remarks>
public enum ContentChangeResult
{
    /// <summary>
    /// The change was made.
    /// </summary>
    Done = 0,

    /// <summary>
    /// Nothing of the kind the change needs is at the location, or the exposure options hide it,
    /// so it is answered as absent: no file to delete, no entry to rename, no directory to
    /// remove.
    /// </summary>
    Absent = 1,

    /// <summary>
    /// The directory to remove holds at least one entry, hidden entries included.
    /// </summary>
    NotEmpty = 2,

    /// <summary>
    /// An entry is in the way: a directory or file where a directory is to be created, or a
    /// directory where an entry is to be renamed to (or a file, when a directory is renamed).
    /// </summary>
    Exists = 3,

    /// <summary>
    /// The directory the new name belongs in does not exist.
    /// </summary>
    NoSuchDirectory = 4,

    /// <summary>
    /// The change is not permitted: uploads are off, the new name is hidden by the exposure
    /// options or lies under <c>/.surl</c>, the served root itself would be removed, renamed or
    /// replaced, a directory would be renamed into itself, or a file renamed to a path that ends
    /// in <c>/</c>.
    /// </summary>
    NotPermitted = 5,
}
