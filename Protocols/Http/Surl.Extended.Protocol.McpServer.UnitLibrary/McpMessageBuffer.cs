namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Holds a Streamable HTTP <c>POST</c>'s body, the one JSON-RPC message, up to the message limit (ADR-0111
/// decision 7.3). Bytes past the limit are not kept; <see cref="IsPastLimit" /> says some arrived.
/// </summary>
/// <param name="limit">The most bytes the message may hold.</param>
internal sealed class McpMessageBuffer(int limit) : Stream
{
    private readonly MemoryStream _kept = new();

    /// <summary>Gets a value indicating whether more bytes arrived than the limit allows.</summary>
    public bool IsPastLimit { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => _kept.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _kept.Length;
        set => throw new NotSupportedException();
    }

    /// <summary>The message's bytes as kept.</summary>
    /// <returns>The bytes.</returns>
    public byte[] ToArray() => _kept.ToArray();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (IsPastLimit || _kept.Length + buffer.Length > limit)
        {
            IsPastLimit = true;
            return;
        }

        _kept.Write(buffer);
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer.AsSpan(offset, count));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _kept.Dispose();
        }

        base.Dispose(disposing);
    }
}
