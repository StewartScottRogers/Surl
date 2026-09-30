using Surl.Content;

namespace Surl.Protocol.Ssh;

/// <summary>
/// What an SFTP handle stands for: a file opened for reading, a directory opened for listing with
/// its listing read once when it was opened (ADR-0054, decision 8), or a file opened for writing,
/// an upload committed at <c>CLOSE</c> (decision 9).
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
    /// For a file read, how many bytes were read through it; for a directory, how many entries
    /// were listed; for an upload, how many bytes were written through it.
    /// </summary>
    public long Progress { get; set; }

    /// <summary>
    /// The upload a handle opened with <c>WRITE</c> writes; <see langword="null"/> for a file
    /// opened for reading only, or a directory.
    /// </summary>
    public ContentUploadSession? Upload { get; init; }

    /// <summary>
    /// Whether an upload was opened with <c>APPEND</c>, so every write lands at its end.
    /// </summary>
    public bool Appends { get; init; }

    /// <summary>
    /// Whether an upload was opened with <c>READ</c> too, so <c>READ</c> sees its bytes.
    /// </summary>
    public bool ReadsUpload { get; init; }

    /// <summary>
    /// The last write time an <c>FSETSTAT</c> asked for, applied once the upload is committed.
    /// </summary>
    public DateTimeOffset? LastWriteTimeAtClose { get; set; }

    /// <summary>
    /// Why the upload was discarded - <c>File too large</c> or <c>Write failed</c>, with the note's
    /// reason - after which every request on the handle but <c>CLOSE</c> gets that answer;
    /// <see langword="null"/> while the upload stands.
    /// </summary>
    public (string Message, string Reason)? Discarded { get; set; }
}
