using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// A write-only <see cref="Stream"/> that hands every byte to an <see cref="IConnection"/>,
/// so the content store can copy a file straight onto the connection.
/// </summary>
/// <remarks>
/// Only asynchronous writes are supported: a synchronous write would block on the network,
/// and reading, seeking and lengths mean nothing here.
/// </remarks>
internal sealed class ConnectionWriteStream : Stream
{
    private readonly IConnection connection;

    /// <summary>
    /// Creates a stream that writes to <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The connection every write goes to.</param>
    public ConnectionWriteStream(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        this.connection = connection;
    }

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException("A connection has no length.");

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException("A connection has no position.");
        set => throw new NotSupportedException("A connection has no position.");
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        connection.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>
    /// Does nothing: every write has already been handed to the connection.
    /// </summary>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Not supported: only asynchronous writes reach the connection.
    /// </summary>
    /// <param name="buffer">Unused.</param>
    /// <param name="offset">Unused.</param>
    /// <param name="count">Unused.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Write to a connection asynchronously.");

    /// <summary>
    /// Not supported: the stream is write-only.
    /// </summary>
    /// <param name="buffer">Unused.</param>
    /// <param name="offset">Unused.</param>
    /// <param name="count">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The stream is write-only.");

    /// <summary>
    /// Not supported: a connection cannot seek.
    /// </summary>
    /// <param name="offset">Unused.</param>
    /// <param name="origin">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("A connection cannot seek.");

    /// <summary>
    /// Not supported: a connection has no length.
    /// </summary>
    /// <param name="value">Unused.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void SetLength(long value) =>
        throw new NotSupportedException("A connection has no length.");
}
