namespace Surl.Networking;

/// <summary>
/// Reads the bytes a <see cref="StreamConnection"/> already read ahead off its transport, then the
/// transport's own bytes, and writes straight to the transport, so a TLS handshake started
/// after a peek or a peer-close watch still sees every byte the client sent first (ADR-0113, decision 6; ADR-0141, amendment 2).
/// </summary>
/// <remarks>Disposing it leaves the transport open; the connection owns and disposes the transport.</remarks>
/// <param name="readAheadBytes">The bytes the connection read ahead and has not handed out, in order.</param>
/// <param name="transport">The stream the bytes were read from, which carries everything after them.</param>
internal sealed class ReadAheadPrefixStream(byte[] readAheadBytes, Stream transport) : Stream
{
    private int readAheadOffset;

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

        var served = TakeReadAheadBytes(buffer.AsSpan(offset, count));

        return served > 0 ? served : transport.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var served = TakeReadAheadBytes(buffer.Span);

        return served > 0 ? served : await transport.ReadAsync(buffer, cancellationToken);
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

    private int TakeReadAheadBytes(Span<byte> destination)
    {
        var served = Math.Min(destination.Length, readAheadBytes.Length - readAheadOffset);

        readAheadBytes.AsSpan(readAheadOffset, served).CopyTo(destination);
        readAheadOffset += served;

        return served;
    }
}
