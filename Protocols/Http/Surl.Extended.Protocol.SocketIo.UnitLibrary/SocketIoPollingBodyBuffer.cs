namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Holds an HTTP long-polling <c>POST</c>'s body up to <c>maxPayload</c> (ADR-0120, decision 7).
/// Bytes past the limit are not kept; <see cref="IsPastLimit"/> says some arrived.
/// </summary>
/// <param name="limit">The most bytes the body may hold; 0 for no limit, as <c>--max-message</c> 0 means.</param>
internal sealed class SocketIoPollingBodyBuffer(long limit) : Stream
{
    private readonly MemoryStream kept = new();

    /// <summary>Whether more bytes arrived than the limit allows.</summary>
    public bool IsPastLimit { get; private set; }

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => kept.Length;

    /// <inheritdoc/>
    public override long Position
    {
        get => kept.Length;
        set => throw new NotSupportedException();
    }

    /// <summary>The body's bytes as kept.</summary>
    public byte[] ToArray() => kept.ToArray();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (IsPastLimit || (limit != 0 && kept.Length + buffer.Length > limit))
        {
            IsPastLimit = true;

            return;
        }

        kept.Write(buffer);
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer.AsSpan(offset, count));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            kept.Dispose();
        }

        base.Dispose(disposing);
    }
}
