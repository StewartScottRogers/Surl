using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// Sends one file to the client in lock step, as <see cref="TftpProtocolServer"/> describes:
/// the OACK if any, then DATA blocks, each sent once the one before is acknowledged, and each
/// sent again when its ACK does not come in time.
/// </summary>
/// <param name="flow">The flow, already moved to the server's transfer port.</param>
/// <param name="context">The exchange: its log, clock and cancellation.</param>
/// <param name="contentStore">Where the file is read from.</param>
/// <param name="mapping">The file's mapped location.</param>
/// <param name="fileLength">The file's length when the transfer began.</param>
/// <param name="negotiation">The block size, retransmission timeout and OACK.</param>
internal sealed class TftpReadTransfer(
    IDatagramFlow flow,
    ExchangeContext context,
    ContentStore contentStore,
    ContentPathMapping mapping,
    long fileLength,
    TftpNegotiation negotiation)
{
    /// <summary>
    /// How many times a packet is sent again before the server gives up on the client.
    /// </summary>
    public const int MaximumRetransmissions = 5;

    private enum AcknowledgementOutcome
    {
        Acknowledged,
        TimedOut,
        ClientError,
        IllegalPacket,
    }

    /// <summary>
    /// Runs the transfer to its end: the last block acknowledged, the client's ERROR, an
    /// illegal packet, the file shrinking, or the retransmissions running out.
    /// </summary>
    /// <returns>A task that completes when the transfer is over.</returns>
    public async Task RunAsync()
    {
        if (negotiation.OptionAcknowledgement is { } optionAcknowledgement
            && !await SendUntilAcknowledgedAsync(optionAcknowledgement, 0))
        {
            return;
        }

        long offset = 0;
        ushort blockNumber = 1;
        while (true)
        {
            var count = (int)Math.Min(negotiation.BlockSize, fileLength - offset);
            if (await ReadDataPacketAsync(blockNumber, offset, count) is not { } packet)
            {
                context.Log.Note($"{mapping.Location} shrank below {offset + count} bytes while it was sent; the transfer was ended with ERROR 0.");
                await SendAsync(TftpPacket.ForError(TftpErrorCode.NotDefined, "The file changed while it was sent."));
                return;
            }

            if (!await SendUntilAcknowledgedAsync(packet, blockNumber))
            {
                return;
            }

            if (count < negotiation.BlockSize)
            {
                context.Log.Note($"All {fileLength} bytes of {mapping.Location} were acknowledged in {offset / negotiation.BlockSize + 1} blocks.");
                return;
            }

            offset += count;
            blockNumber = unchecked((ushort)(blockNumber + 1));
        }
    }

    private async Task<byte[]?> ReadDataPacketAsync(ushort blockNumber, long offset, int count)
    {
        if (count == 0)
        {
            return TftpPacket.ForData(blockNumber, []);
        }

        using var block = new MemoryStream(count);
        var copied = await contentStore.CopyFileBytesAsync(
            mapping,
            ContentByteRange.Select(fileLength, offset, offset + count - 1),
            block,
            context.CancellationToken);

        return copied < count ? null : TftpPacket.ForData(blockNumber, block.GetBuffer().AsSpan(0, count));
    }

    private async Task<bool> SendUntilAcknowledgedAsync(byte[] packet, ushort blockNumber)
    {
        for (var sends = 0; sends <= MaximumRetransmissions; sends++)
        {
            await SendAsync(packet);
            switch (await AwaitAcknowledgementAsync(blockNumber))
            {
                case AcknowledgementOutcome.Acknowledged:
                    return true;
                case AcknowledgementOutcome.ClientError:
                    context.Log.Note($"The client ended the transfer with an ERROR while block {blockNumber} awaited its ACK.");
                    return false;
                case AcknowledgementOutcome.IllegalPacket:
                    context.Log.Note($"The client sent a packet that is neither an ACK nor an ERROR while block {blockNumber} awaited its ACK; the transfer was ended with ERROR 4.");
                    await SendAsync(TftpPacket.ForError(TftpErrorCode.IllegalOperation, "Illegal TFTP operation"));
                    return false;
            }
        }

        context.Log.Note($"Block {blockNumber} was not acknowledged after {MaximumRetransmissions} retransmissions, {negotiation.RetransmissionTimeout.TotalSeconds} seconds apart; the transfer was abandoned.");
        return false;
    }

    private async Task<AcknowledgementOutcome> AwaitAcknowledgementAsync(ushort blockNumber)
    {
        using var timeout = new CancellationTokenSource(negotiation.RetransmissionTimeout, context.TimeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeout.Token);
        try
        {
            while (true)
            {
                var datagram = await flow.ReceiveAsync(linked.Token);
                if (Classify(datagram.Span, blockNumber) is { } outcome)
                {
                    return outcome;
                }
            }
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return AcknowledgementOutcome.TimedOut;
        }
    }

    /// <summary>
    /// What a datagram from the client means while <paramref name="blockNumber"/> awaits its
    /// ACK; <see langword="null"/> for an ACK of another block, such as a duplicate ACK of
    /// the block before, which is ignored so that it never triggers a second copy of a block
    /// (RFC 1123 section 4.2.3.1).
    /// </summary>
    private static AcknowledgementOutcome? Classify(ReadOnlySpan<byte> datagram, ushort blockNumber) =>
        (TftpPacket.ReadOpcode(datagram), TftpPacket.ReadBlockNumber(datagram)) switch
        {
            (TftpPacket.Acknowledgement, var block) when block == blockNumber => AcknowledgementOutcome.Acknowledged,
            (TftpPacket.Acknowledgement, >= 0) => null,
            (TftpPacket.Error, _) => AcknowledgementOutcome.ClientError,
            _ => AcknowledgementOutcome.IllegalPacket,
        };

    private ValueTask SendAsync(byte[] packet) => flow.SendAsync(packet, context.CancellationToken);
}
