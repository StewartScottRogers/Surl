using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Dict;

/// <summary>
/// A write-only <see cref="Stream"/> that sends the bytes written to it as the text of a DICT
/// textual response (RFC 2229, section 2.4.1), so the content store can copy a file straight
/// onto the connection.
/// </summary>
/// <remarks>
/// Every line ending, LF, CRLF or a lone CR, goes out as CRLF, and a line that starts with
/// <c>.</c> has another <c>.</c> put before it. <see cref="EndTextAsync"/> ends the last line
/// if the text did not, then sends the terminating <c>.</c> line. Only asynchronous writes
/// are supported: a synchronous write would block on the network, and reading, seeking and
/// lengths mean nothing here.
/// </remarks>
internal sealed class DictTextWriteStream : Stream
{
    private static readonly byte[] TextTerminator = ".\r\n"u8.ToArray();

    private readonly IConnection connection;
    private bool atLineStart = true;
    private bool pendingCarriageReturn;
    private byte[] output = [];
    private int outputCount;

    /// <summary>
    /// Creates a stream that writes text to <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">The connection every write goes to.</param>
    public DictTextWriteStream(IConnection connection)
    {
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
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        StartOutput((buffer.Length * 2) + 2);
        foreach (var value in buffer.Span)
        {
            AppendTransformed(value);
        }

        return connection.WriteAsync(output.AsMemory(0, outputCount), cancellationToken);
    }

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>
    /// Ends the last line if the text did not, then sends the <c>.</c> line that ends the text.
    /// </summary>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes once the bytes have been handed to the connection.</returns>
    public async ValueTask EndTextAsync(CancellationToken cancellationToken)
    {
        StartOutput(2);
        if (pendingCarriageReturn || !atLineStart)
        {
            AppendLineEnd();
        }

        await connection.WriteAsync(output.AsMemory(0, outputCount), cancellationToken);
        await connection.WriteAsync(TextTerminator, cancellationToken);
    }

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

    private void StartOutput(int capacity)
    {
        if (output.Length < capacity)
        {
            output = new byte[capacity];
        }

        outputCount = 0;
    }

    private void AppendTransformed(byte value)
    {
        if (TakePendingCarriageReturn(value))
        {
            return;
        }

        switch (value)
        {
            case (byte)'\r':
                pendingCarriageReturn = true;
                break;
            case (byte)'\n':
                AppendLineEnd();
                break;
            default:
                AppendText(value);
                break;
        }
    }

    private bool TakePendingCarriageReturn(byte value)
    {
        if (!pendingCarriageReturn)
        {
            return false;
        }

        pendingCarriageReturn = false;
        AppendLineEnd();

        return value == (byte)'\n';
    }

    private void AppendText(byte value)
    {
        if (atLineStart && value == (byte)'.')
        {
            output[outputCount++] = (byte)'.';
        }

        output[outputCount++] = value;
        atLineStart = false;
    }

    private void AppendLineEnd()
    {
        output[outputCount++] = (byte)'\r';
        output[outputCount++] = (byte)'\n';
        atLineStart = true;
        pendingCarriageReturn = false;
    }
}
