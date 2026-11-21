namespace Surl.Protocol.Smtp;

/// <summary>
/// A write-only stream that writes every byte to each of its targets, so one body read fills
/// every owner's own pending message (ADR-0053 Amendment 1, decision 4).
/// </summary>
/// <param name="targets">The streams every write goes to, in order.</param>
internal sealed class SmtpTeeStream(IReadOnlyList<Stream> targets) : Stream
{
    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
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
    public override void Write(byte[] buffer, int offset, int count)
    {
        foreach (var target in targets)
        {
            target.Write(buffer, offset, count);
        }
    }

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        foreach (var target in targets)
        {
            await target.WriteAsync(buffer, cancellationToken);
        }
    }
}
