namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A write-only stream that keeps a <c>POST</c>ed DNS over HTTPS query up to a limit and says
/// when the body went past it, keeping nothing more, so a body too large for a DNS message is
/// never held whole (ADR-0092 decision 5's <c>413</c>).
/// </summary>
/// <param name="limit">The most bytes kept.</param>
internal sealed class DnsOverHttpsQueryBuffer(int limit) : Stream
{
    private readonly MemoryStream kept = new();

    /// <summary>Whether a write would have taken the body past the limit.</summary>
    public bool IsPastLimit { get; private set; }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => kept.Length;

    public override long Position
    {
        get => kept.Length;
        set => throw new NotSupportedException();
    }

    /// <summary>The bytes kept.</summary>
    public byte[] ToArray() => kept.ToArray();

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (IsPastLimit || kept.Length + buffer.Length > limit)
        {
            IsPastLimit = true;
            return;
        }

        kept.Write(buffer);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer.AsSpan(offset, count));
        return Task.CompletedTask;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            kept.Dispose();
        }

        base.Dispose(disposing);
    }
}
