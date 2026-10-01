namespace Surl.MailStore;

/// <summary>
/// The write-only stream a <see cref="PendingMessage"/>'s body is written through. It counts
/// every byte written, and passes them on to <paramref name="destination"/> - the pending file,
/// or memory for a store without files - until either the destination fails or the count
/// passes <paramref name="writeLimit"/>, after which it counts and drops them. A write never
/// throws for a failing destination, so a server can read the rest of the body off the wire
/// and answer the refusal in its own words (ADR-0050, decision 7).
/// </summary>
/// <param name="destination">Where the bytes are kept.</param>
/// <param name="writeLimit">The most bytes worth keeping: a longer body cannot be stored.</param>
/// <param name="failure">Why the destination cannot be written, when that is known already;
/// otherwise <see langword="null"/>.</param>
internal sealed class PendingMessageStream(Stream destination, long writeLimit, string? failure) : Stream
{
    private bool closed;

    /// <summary>
    /// How many bytes have been written, kept or not.
    /// </summary>
    public long BytesWritten { get; private set; }

    /// <summary>
    /// The message of the exception the destination threw; <see langword="null"/> while it has
    /// thrown none.
    /// </summary>
    public string? Failure { get; private set; } = failure;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => !closed;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        // The destination is flushed when it is closed.
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        Write(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(closed, this);
        if (Keeps(buffer.Length))
        {
            var bytes = buffer.ToArray();
            Failure = MailStoreFiles.CatchStorageFailure(() => destination.Write(bytes));
        }

        BytesWritten += buffer.Length;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(closed, this);
        if (Keeps(buffer.Length))
        {
            Failure = await MailStoreFiles.CatchStorageFailureAsync(() => destination.WriteAsync(buffer, cancellationToken).AsTask());
        }

        BytesWritten += buffer.Length;
    }

    /// <summary>
    /// Closes the destination, flushing what it holds; a failure to is kept as
    /// <see cref="Failure"/> unless one is kept already. Closing again does nothing.
    /// </summary>
    public void CloseDestination()
    {
        if (!closed)
        {
            closed = true;
            Failure ??= MailStoreFiles.CatchStorageFailure(destination.Dispose);
        }
    }

    protected override void Dispose(bool disposing)
    {
        CloseDestination();
        base.Dispose(disposing);
    }

    private bool Keeps(int count) => Failure is null && BytesWritten + count <= writeLimit;
}
