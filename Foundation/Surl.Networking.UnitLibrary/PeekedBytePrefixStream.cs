namespace Surl.Networking;

/// <summary>
/// Reads one byte a <see cref="StreamConnection"/> already peeked off its transport, then the
/// transport's own bytes, and writes straight to the transport, so a TLS handshake started
/// after a peek still sees the ClientHello's first byte (ADR-0113, decision 6).
/// </summary>
/// <remarks>Disposing it leaves the transport open; the connection owns and disposes the transport.</remarks>
/// <param name="peekedByte">The byte the connection peeked and has not handed out.</param>
/// <param name="transport">The stream the byte was read from, which carries everything after it.</param>
internal sealed class PeekedBytePrefixStream(byte peekedByte, Stream transport) : Stream
{
    private bool peekedByteRead;

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

        return TryReadPeekedByte(buffer.AsSpan(offset, count)) ? 1 : transport.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        TryReadPeekedByte(buffer.Span) ? 1 : await transport.ReadAsync(buffer, cancellationToken);

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

    private bool TryReadPeekedByte(Span<byte> destination)
    {
        if (peekedByteRead || destination.IsEmpty)
        {
            return false;
        }

        destination[0] = peekedByte;
        peekedByteRead = true;

        return true;
    }
}
