namespace Surl.MailStore;

/// <summary>
/// A message body on its way into the store (ADR-0050, decision 7), made by
/// <see cref="MailboxStore.CreatePendingMessage"/>. A server writes the body to
/// <see cref="Body"/> as it reads it off the wire, then hands this to
/// <see cref="MailboxStore.Deliver(IReadOnlyList{MailRecipient}, PendingMessage)"/> or
/// <see cref="MailboxStore.Append(MailView, string, PendingMessage, MailFlags, DateTimeOffset?, out MailStoredUid)"/>,
/// which stores it or refuses it, and disposes it either way.
/// </summary>
/// <remarks>
/// <para>
/// With a data directory the body is streamed straight into a pending file,
/// <c>messages/.pending-&lt;guid&gt;</c>, renamed to its message file number when the message is
/// stored. Without one it is held in memory.
/// </para>
/// <para>
/// A pending file that cannot be created or written does not throw: the rest of the body is
/// counted and dropped, <see cref="StorageFailure"/> holds the exception's message, and the
/// delivery or append is refused with <see cref="MailStoreOutcome.StorageFailed"/>. A body
/// longer than the store could ever keep is counted and dropped past that length in the same
/// way, and refused by its size.
/// </para>
/// <para>
/// Disposing it without storing it abandons it: the pending file is deleted. A pending file that
/// cannot be deleted is left behind, and ignored at the next load.
/// </para>
/// </remarks>
public sealed class PendingMessage : IDisposable
{
    private readonly MailStoreFiles? files;
    private readonly MemoryStream? memory;
    private readonly PendingMessageStream body;
    private string? pendingPath;
    private string? keepFailure;
    private bool closed;

    internal PendingMessage(MailStoreFiles? files, long writeLimit)
    {
        this.files = files;
        if (files is null)
        {
            memory = new MemoryStream();
            body = new PendingMessageStream(memory, writeLimit, failure: null);
            return;
        }

        Stream? file = null;
        var failure = MailStoreFiles.CatchStorageFailure(() => file = files.CreatePendingMessage(out pendingPath));
        body = new PendingMessageStream(file ?? Stream.Null, writeLimit, failure);
    }

    /// <summary>
    /// The stream the message's bytes are written to, exactly as they are to be stored. It is
    /// write-only, and a write to it never throws for a failing pending file.
    /// </summary>
    public Stream Body => body;

    /// <summary>
    /// How many bytes have been written to <see cref="Body"/>.
    /// </summary>
    public long Length => body.BytesWritten;

    /// <summary>
    /// Why the pending file cannot be created, written or renamed into place: the message of the
    /// exception the file system threw. <see langword="null"/> while nothing has failed.
    /// </summary>
    public string? StorageFailure => keepFailure ?? body.Failure;

    /// <summary>
    /// Abandons the message unless it was stored: the pending file is closed and deleted.
    /// Disposing it again does nothing.
    /// </summary>
    public void Dispose()
    {
        closed = true;
        body.CloseDestination();
        var path = pendingPath;
        pendingPath = null;
        if (path is not null)
        {
            // Left behind when it cannot be deleted: the next load ignores it.
            MailStoreFiles.CatchStorageFailure(() => files!.DeletePendingMessage(path));
        }
    }

    /// <summary>
    /// Ends the writing, once: the pending file is closed, so its bytes are all in it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The message was stored, refused or disposed already.</exception>
    internal void Close()
    {
        if (closed)
        {
            throw new InvalidOperationException("The pending message has already been stored, refused or disposed.");
        }

        closed = true;
        body.CloseDestination();
    }

    /// <summary>
    /// Keeps the closed message's bytes as message file <paramref name="fileNumber"/>: renames
    /// the pending file to it, or takes the bytes from memory.
    /// </summary>
    /// <param name="fileNumber">The message file number the bytes are kept under.</param>
    /// <param name="bytes">The bytes held in memory for a store without files; otherwise
    /// <see langword="null"/>.</param>
    /// <returns>Whether they were kept; when not, <see cref="StorageFailure"/> says why.</returns>
    internal bool TryKeep(ulong fileNumber, out byte[]? bytes)
    {
        bytes = memory?.ToArray();
        if (files is null)
        {
            return true;
        }

        keepFailure = MailStoreFiles.CatchStorageFailure(() => files.KeepPendingMessage(pendingPath!, fileNumber));
        pendingPath = keepFailure is null ? null : pendingPath;
        return keepFailure is null;
    }
}
