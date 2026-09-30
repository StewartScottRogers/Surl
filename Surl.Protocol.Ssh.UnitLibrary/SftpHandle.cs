using Surl.Content;

namespace Surl.Protocol.Ssh;

/// <summary>
/// What an SFTP handle stands for: a file opened for reading, or a directory opened for listing
/// with its listing read once when it was opened (ADR-0054, decision 8).
/// </summary>
/// <param name="path">The canonical path it was opened with, for the notes.</param>
/// <param name="mapping">Where the path maps in the content store.</param>
/// <param name="entries">A directory's entries; <see langword="null"/> for a file.</param>
internal sealed class SftpHandle(string path, ContentPathMapping mapping, IReadOnlyList<ContentDirectoryEntry>? entries)
{
    /// <summary>
    /// The canonical path it was opened with.
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Where the path maps in the content store.
    /// </summary>
    public ContentPathMapping Mapping { get; } = mapping;

    /// <summary>
    /// A directory's entries; <see langword="null"/> for a file.
    /// </summary>
    public IReadOnlyList<ContentDirectoryEntry>? Entries { get; } = entries;

    /// <summary>
    /// For a file, how many bytes were read through it; for a directory, how many entries were listed.
    /// </summary>
    public long Progress { get; set; }
}
