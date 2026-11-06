namespace Surl.Protocol.Tftp;

/// <summary>
/// The bytes of a write request's DATA blocks as a read-only stream the content store's
/// upload path reads to its end. Each block is asked for only when the store has taken every
/// byte of the one before, by sending the packet that acknowledges it: the OACK or ACK 0
/// first, then ACK 1, 2 and on (RFC 1350 section 6). Nothing is sent until the store's first
/// read, so a store that refuses the upload is answered before any ACK.
/// </summary>
/// <param name="lockStep">The flow's lock step.</param>
/// <param name="openingPacket">The OACK, or ACK 0 without one: what asks for DATA 1.</param>
/// <param name="blockSize">The negotiated block size; the first shorter block is the last.</param>
internal sealed class TftpUploadStream(TftpLockStep lockStep, byte[] openingPacket, int blockSize) : Stream
{
    private ReadOnlyMemory<byte> unread = ReadOnlyMemory<byte>.Empty;
    private ushort awaitedBlockNumber = 1;
    private bool isLastBlockReceived;

    /// <summary>
    /// The packet that acknowledges every block received so far: after the last block, the
    /// final ACK the server sends once the upload is written.
    /// </summary>
    public byte[] Acknowledgement { get; private set; } = openingPacket;

    /// <summary>The DATA blocks received so far.</summary>
    public long BlocksReceived { get; private set; }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Copies bytes of the block received last, asking for the next block first when every
    /// byte of it has been taken.
    /// </summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Unused: the lock step honours the exchange's cancellation.</param>
    /// <returns>The bytes copied; 0 once the last block has been taken.</returns>
    /// <exception cref="TftpTransferEndedException">The transfer ended before the last block arrived.</exception>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        if (unread.IsEmpty && !isLastBlockReceived)
        {
            unread = await ReceiveNextBlockAsync();
        }

        var count = Math.Min(buffer.Length, unread.Length);
        unread[..count].CopyTo(buffer);
        unread = unread[count..];
        return count;
    }

    /// <summary>
    /// Copies bytes as <see cref="ReadAsync(Memory{byte}, CancellationToken)"/> does.
    /// </summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="offset">Where in <paramref name="buffer"/> the first byte goes.</param>
    /// <param name="count">The most bytes to copy.</param>
    /// <param name="cancellationToken">Unused: the lock step honours the exchange's cancellation.</param>
    /// <returns>The bytes copied; 0 once the last block has been taken.</returns>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>Not supported: the upload is read asynchronously.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private async Task<ReadOnlyMemory<byte>> ReceiveNextBlockAsync()
    {
        var data = await lockStep.SendUntilAnsweredAsync(Acknowledgement, TftpPacket.Data, awaitedBlockNumber, $"DATA {awaitedBlockNumber} was awaited")
            ?? throw new TftpTransferEndedException();
        var payload = data[4..];
        Acknowledgement = TftpPacket.ForAcknowledgement(awaitedBlockNumber);
        awaitedBlockNumber = unchecked((ushort)(awaitedBlockNumber + 1));
        isLastBlockReceived = payload.Length < blockSize;
        BlocksReceived++;
        return payload;
    }
}
