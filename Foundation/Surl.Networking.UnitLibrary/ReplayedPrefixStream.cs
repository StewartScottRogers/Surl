namespace Surl.Networking;

/// <summary>
/// Reads the bytes already taken off a transport, then the transport's own, and writes straight to
/// the transport, so a handshake started after a look at the ClientHello still sees every byte
/// (ADR-0157, decision 2).
/// </summary>
/// <remarks>Disposing it leaves the transport open; the connection owns and disposes the transport.</remarks>
/// <param name="prefix">The bytes read from the transport and not yet handed out.</param>
/// <param name="transport">The stream the bytes were read from, which carries everything after them.</param>
internal sealed class ReplayedPrefixStream(byte[] prefix, Stream transport) : Stream
{
    private int prefixOffset;

    /// <inheritdoc/>
    public override bool CanRead => true;

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
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        var copied = CopyPrefix(buffer.AsSpan(offset, count));

        return copied ?? transport.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var copied = CopyPrefix(buffer.Span);

        return copied ?? await transport.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => transport.Write(buffer, offset, count);

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        transport.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        transport.WriteAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc/>
    public override void Flush() => transport.Flush();

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) => transport.FlushAsync(cancellationToken);

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    // Null once the prefix is spent. An empty read while bytes remain returns 0 at once, because a
    // zero-length read means "wait until data is available" and the prefix is data.
    private int? CopyPrefix(Span<byte> destination)
    {
        if (prefixOffset == prefix.Length)
        {
            return null;
        }

        var count = Math.Min(destination.Length, prefix.Length - prefixOffset);
        prefix.AsSpan(prefixOffset, count).CopyTo(destination);
        prefixOffset += count;

        return count;
    }
}
