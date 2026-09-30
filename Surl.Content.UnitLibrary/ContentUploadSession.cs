namespace Surl.Content;

/// <summary>
/// A random-access upload <see cref="ContentStore.OpenUploadAsync(ContentPathMapping, ContentUploadOpening, CancellationToken)"/>
/// opened: a temporary file beside the target, written and read at any offset and resized,
/// until it is committed over the target or discarded (ADR-0054 decision 14 item 4).
/// </summary>
/// <remarks>
/// <para>
/// The temporary file is a dot-file beside the target, so it is neither served nor listed by
/// default, and the target keeps its bytes until <see cref="CommitAsync(CancellationToken)"/>
/// renames the temporary file over it. Disposing the session without a commit discards the
/// upload: the temporary file is deleted and the target left as it was (ADR-0006 section 5).
/// </para>
/// <para>
/// A write or <see cref="SetLengthAsync(long)"/> that would take <see cref="Length"/> past
/// <see cref="ContentExposureOptions.MaxUploadBytes"/> (0 for no limit) keeps none of its
/// bytes and ends the session as <see cref="ContentUploadResult.TooLarge"/>: the temporary file
/// is deleted, and every later write, resize or commit answers
/// <see cref="ContentUploadResult.TooLarge"/> again. A write or resize that throws, cancellation
/// included, ends the session the same way before the exception goes on; every later call but
/// <see cref="DisposeAsync"/> then throws <see cref="InvalidOperationException"/>, as it does
/// after a commit.
/// </para>
/// <para>
/// Make one call at a time: the session is not safe for concurrent use.
/// </para>
/// </remarks>
public sealed class ContentUploadSession : IAsyncDisposable
{
    private readonly IContentFileSystem fileSystem;
    private readonly string temporaryLocation;
    private readonly string targetLocation;
    private readonly long maxUploadBytes;
    private readonly Stream temporaryFile;
    private SessionState state = SessionState.Open;

    internal ContentUploadSession(IContentFileSystem fileSystem, string temporaryLocation, string targetLocation, Stream temporaryFile, long maxUploadBytes)
    {
        this.fileSystem = fileSystem;
        this.temporaryLocation = temporaryLocation;
        this.targetLocation = targetLocation;
        this.temporaryFile = temporaryFile;
        this.maxUploadBytes = maxUploadBytes;
        Length = temporaryFile.Length;
    }

    private enum SessionState
    {
        Open,
        TooLarge,
        Ended,
        Disposed,
    }

    /// <summary>
    /// The upload's length in bytes: what it started from, as the writes and resizes since have
    /// left it.
    /// </summary>
    public long Length { get; private set; }

    /// <summary>
    /// Writes <paramref name="bytes"/> at <paramref name="offset"/>, filling any gap between the
    /// upload's end and <paramref name="offset"/> with zero bytes.
    /// </summary>
    /// <param name="offset">Where the first byte lands, at least 0.</param>
    /// <param name="bytes">The bytes to write.</param>
    /// <param name="cancellationToken">Cancels the write, which ends the session.</param>
    /// <returns><see cref="ContentUploadResult.Written"/>, or
    /// <see cref="ContentUploadResult.TooLarge"/> when the upload would pass the limit or already
    /// has.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">The upload was committed or its session ended
    /// by an exception.</exception>
    /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
    public async Task<ContentUploadResult> WriteAtAsync(long offset, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (state == SessionState.TooLarge)
        {
            return ContentUploadResult.TooLarge;
        }

        RequireOpen();
        long newLength = Math.Max(Length, checked(offset + bytes.Length));
        if (!IsWithinUploadLimit(newLength))
        {
            return await EndAsTooLargeAsync();
        }

        try
        {
            temporaryFile.Seek(offset, SeekOrigin.Begin);
            await temporaryFile.WriteAsync(bytes, cancellationToken);
        }
        catch (Exception)
        {
            Discard(SessionState.Ended);
            throw;
        }

        Length = newLength;
        return ContentUploadResult.Written;
    }

    /// <summary>
    /// Reads the upload's bytes from <paramref name="offset"/> into <paramref name="buffer"/>,
    /// until the buffer is full or the upload ends.
    /// </summary>
    /// <param name="offset">Where to start reading, at least 0; at or past
    /// <see cref="Length"/> nothing is read.</param>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>How many bytes were read: fewer than the buffer holds only at the upload's
    /// end.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">The upload was committed, or its session
    /// ended as too large or by an exception.</exception>
    /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
    public async Task<int> ReadAtAsync(long offset, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        RequireOpen();
        if (offset >= Length)
        {
            return 0;
        }

        temporaryFile.Seek(offset, SeekOrigin.Begin);
        return await temporaryFile.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
    }

    /// <summary>
    /// Makes the upload <paramref name="length"/> bytes long: cut at that length, or grown to it
    /// with zero bytes.
    /// </summary>
    /// <param name="length">The new length, at least 0.</param>
    /// <returns><see cref="ContentUploadResult.Written"/>, or
    /// <see cref="ContentUploadResult.TooLarge"/> when the length passes the limit or the upload
    /// already has.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">The upload was committed or its session ended
    /// by an exception.</exception>
    /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
    public async Task<ContentUploadResult> SetLengthAsync(long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (state == SessionState.TooLarge)
        {
            return ContentUploadResult.TooLarge;
        }

        RequireOpen();
        if (!IsWithinUploadLimit(length))
        {
            return await EndAsTooLargeAsync();
        }

        try
        {
            temporaryFile.SetLength(length);
        }
        catch (Exception)
        {
            Discard(SessionState.Ended);
            throw;
        }

        Length = length;
        return ContentUploadResult.Written;
    }

    /// <summary>
    /// Renames the temporary file over the target, replacing any file there, and ends the session.
    /// </summary>
    /// <remarks>
    /// When a directory has appeared at the target, or its directory has gone, since the upload
    /// was opened, the temporary file is deleted and the target left as it is. A rename that
    /// throws deletes the temporary file too before the exception goes on.
    /// </remarks>
    /// <param name="cancellationToken">Checked before the temporary file is closed.</param>
    /// <returns><see cref="ContentUploadResult.Written"/>;
    /// <see cref="ContentUploadResult.TooLarge"/> when the session ended as too large;
    /// <see cref="ContentUploadResult.NotPermitted"/> when the target can no longer take a
    /// file.</returns>
    /// <exception cref="InvalidOperationException">The upload was committed or its session ended
    /// by an exception.</exception>
    /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
    public async Task<ContentUploadResult> CommitAsync(CancellationToken cancellationToken)
    {
        if (state == SessionState.TooLarge)
        {
            return ContentUploadResult.TooLarge;
        }

        RequireOpen();
        cancellationToken.ThrowIfCancellationRequested();
        state = SessionState.Ended;
        try
        {
            await temporaryFile.DisposeAsync();
            if (!CanTargetTakeAFile())
            {
                fileSystem.DeleteFile(temporaryLocation);
                return ContentUploadResult.NotPermitted;
            }

            fileSystem.MoveFileReplacing(temporaryLocation, targetLocation);
            return ContentUploadResult.Written;
        }
        catch (Exception)
        {
            fileSystem.DeleteFile(temporaryLocation);
            throw;
        }
    }

    /// <summary>
    /// Ends the session: an upload neither committed nor already discarded is discarded, its
    /// temporary file deleted. Disposing twice does nothing more.
    /// </summary>
    /// <returns>A task that completes when the upload is discarded.</returns>
    public async ValueTask DisposeAsync()
    {
        if (state == SessionState.Open)
        {
            await DiscardAsync(SessionState.Disposed);
        }

        state = SessionState.Disposed;
    }

    private void RequireOpen()
    {
        ObjectDisposedException.ThrowIf(state == SessionState.Disposed, this);
        if (state != SessionState.Open)
        {
            throw new InvalidOperationException("The upload has ended: it was committed, too large, or failed.");
        }
    }

    private bool IsWithinUploadLimit(long length) => maxUploadBytes == 0 || length <= maxUploadBytes;

    private bool CanTargetTakeAFile()
    {
        ReadOnlySpan<char> parent = Path.GetDirectoryName(targetLocation.AsSpan());
        return fileSystem.GetEntryKind(targetLocation) != ContentEntryKind.Directory
            && fileSystem.GetEntryKind(parent.ToString()) == ContentEntryKind.Directory;
    }

    private async Task<ContentUploadResult> EndAsTooLargeAsync()
    {
        await DiscardAsync(SessionState.TooLarge);
        return ContentUploadResult.TooLarge;
    }

    // The synchronous twin of DiscardAsync, for catch blocks: an await there makes the compiler
    // rethrow through a branch no exception can take, which coverage counts as missed.
    private void Discard(SessionState ending)
    {
        state = ending;
        try
        {
            temporaryFile.Dispose();
        }
        finally
        {
            fileSystem.DeleteFile(temporaryLocation);
        }
    }

    private async Task DiscardAsync(SessionState ending)
    {
        state = ending;
        try
        {
            await temporaryFile.DisposeAsync();
        }
        finally
        {
            fileSystem.DeleteFile(temporaryLocation);
        }
    }
}
