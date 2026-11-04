using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// Receives one upload from the client in lock step, as <see cref="TftpProtocolServer"/>
/// describes, and writes it through the content store's upload path.
/// </summary>
/// <param name="flow">The flow, already moved to the server's transfer port.</param>
/// <param name="context">The exchange: its log, clock and cancellation.</param>
/// <param name="contentStore">Where the upload is written.</param>
/// <param name="mapping">The upload's mapped location.</param>
/// <param name="negotiation">The block size, retransmission timeout and OACK.</param>
/// <param name="label">What the log calls the request, e.g. <c>Write of "up.txt" (octet)</c>.</param>
internal sealed class TftpWriteTransfer(
    IDatagramFlow flow,
    ExchangeContext context,
    ContentStore contentStore,
    ContentPathMapping mapping,
    TftpNegotiation negotiation,
    string label)
{
    private readonly TftpLockStep lockStep = new(flow, context, negotiation.RetransmissionTimeout);

    /// <summary>
    /// Runs the transfer to its end: the upload written and its last block acknowledged, the
    /// store refusing it (ERROR 2) or finding it past the upload limit (ERROR 3), or the
    /// client's ERROR, an illegal packet or the retransmissions running out. Every end but
    /// the first leaves no file behind.
    /// </summary>
    /// <returns>A task that completes when the transfer is over.</returns>
    public async Task RunAsync()
    {
        await using var upload = new TftpUploadStream(
            lockStep,
            negotiation.OptionAcknowledgement ?? TftpPacket.ForAcknowledgement(0),
            negotiation.BlockSize);
        ContentUploadResult result;
        try
        {
            result = await contentStore.WriteUploadAsync(mapping, upload, context.CancellationToken);
        }
        catch (TftpTransferEndedException)
        {
            context.Log.Note($"{label}: the transfer ended after {upload.BlocksReceived} blocks; the partial file was deleted.");
            return;
        }
        catch (IOException)
        {
            context.Log.Note($"{label}: writing {mapping.Location} failed in block {upload.BlocksReceived}; the partial file was deleted and the transfer ended with ERROR 3.");
            await lockStep.SendAsync(TftpPacket.ForError(TftpErrorCode.DiskFull, TftpProtocolServer.DiskFullMessage));
            return;
        }

        await AnswerAsync(result, upload);
    }

    private async Task AnswerAsync(ContentUploadResult result, TftpUploadStream upload)
    {
        switch (result)
        {
            case ContentUploadResult.Written:
                await lockStep.SendAsync(upload.Acknowledgement);
                context.Log.Note($"{label}: {upload.BlocksReceived} blocks were written to {mapping.Location} and acknowledged.");
                break;
            case ContentUploadResult.TooLarge:
                context.Log.Note($"{label}: the upload grew past the limit of {contentStore.ExposureOptions.MaxUploadBytes} bytes in block {upload.BlocksReceived}; the partial file was deleted and the transfer ended with ERROR 3.");
                await lockStep.SendAsync(TftpPacket.ForError(TftpErrorCode.DiskFull, TftpProtocolServer.DiskFullMessage));
                break;
            default:
                context.Log.Note($"{label}: the content store does not permit an upload to {mapping.Location}; answered with ERROR 2.");
                await lockStep.SendAsync(TftpPacket.ForError(TftpErrorCode.AccessViolation, TftpProtocolServer.AccessViolationMessage));
                break;
        }
    }
}
