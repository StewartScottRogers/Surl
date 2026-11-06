namespace Surl.Conformance;

/// <summary>
/// A writable stand-in for curl's stdin pipe that throws a chosen exception on writing,
/// on flushing or closing, or both - as a pipe does once its reader has exited.
/// </summary>
/// <param name="writeFailure">What a write throws; <see langword="null"/> to accept the bytes.</param>
/// <param name="closeFailure">What a flush or close throws; <see langword="null"/> to close quietly.</param>
internal sealed class ClosedPipeStream(Exception? writeFailure, Exception? closeFailure) : Stream
{
    private bool closeAttempted;

    /// <summary>Gets the bytes the stream accepted.</summary>
    public MemoryStream Written { get; } = new();

    /// <summary>Gets a value indicating whether anything tried to close the stream.</summary>
    public bool CloseAttempted => closeAttempted;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        if (closeFailure is not null)
        {
            throw closeFailure;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (writeFailure is not null)
        {
            throw writeFailure;
        }

        Written.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        closeAttempted = true;
        if (closeFailure is not null)
        {
            throw closeFailure;
        }

        base.Dispose(disposing);
    }
}
