using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// The TFTP server (RFC 1350, with options from RFC 2347 to RFC 2349): answers the request
/// that opened a datagram flow from a new transfer port, serving reads from the content
/// store. ADR-0013 records the answers below.
/// </summary>
/// <remarks>
/// <para>
/// Every reply leaves from a new local port, the server's transfer identifier (RFC 1350
/// section 4): the flow is moved to it before the first reply.
/// </para>
/// <para>
/// A read request (<c>RRQ</c>) names a file, read as a percent-encoded path with <c>%</c>
/// and every byte outside printable ASCII encoded and one leading <c>/</c> dropped, and
/// mapped by the content store. When it maps to a file, the file is sent: first an OACK
/// when the request carried an option the server accepts (<c>blksize</c> from 8, capped at
/// 65464; <c>timeout</c> from 1 to 255 seconds; <c>tsize</c>, answered with the file's
/// length), which the client acknowledges with ACK 0; then DATA 1, 2 and on, each sent once
/// the one before is acknowledged, the block number wrapping from 65535 to 0. A file whose
/// length is a multiple of the block size ends with an empty DATA packet (RFC 1350
/// section 6). Both modes, <c>octet</c> and <c>netascii</c>, send the file's bytes exactly
/// as stored, because upstream curl writes the DATA bytes it receives unchanged in either.
/// </para>
/// <para>
/// A packet is sent again when its ACK does not arrive within the retransmission timeout:
/// the negotiated <c>timeout</c>, or 5 seconds without one, on the exchange's
/// <see cref="ExchangeContext.TimeProvider"/>. After 5 retransmissions the transfer is
/// abandoned with no further packet. An ACK of another block, such as a duplicate ACK of
/// the block before, is ignored and never triggers a resend. An ERROR from the client ends
/// the transfer; any other packet ends it with ERROR 4, <c>Illegal TFTP operation</c>. If
/// the file shrinks while it is sent, the transfer ends with ERROR 0,
/// <c>The file changed while it was sent.</c>
/// </para>
/// <para>
/// A file name the content store refuses, one where nothing exists, one that names a
/// directory, and one that vanishes before it is read, are all answered with ERROR 1,
/// <c>File not found</c>, so a client cannot tell them apart (ADR-0006 section 2); upstream
/// curl exits 68. A write request (<c>WRQ</c>, <c>curl -T</c>) is answered with ERROR 2,
/// <c>Access violation</c>, because uploads are off (ADR-0006 section 2); upstream curl
/// exits 69. A first datagram that is an ERROR gets no reply; any other first datagram that
/// is not a well-formed read request (no final zero byte, an option without a value, or a
/// mode other than <c>octet</c> or <c>netascii</c>) is answered with ERROR 4,
/// <c>Illegal TFTP operation</c>.
/// </para>
/// </remarks>
public sealed class TftpProtocolServer : IDatagramProtocolServer
{
    private readonly ContentStore contentStore;

    /// <summary>
    /// Creates a TFTP server that serves <paramref name="contentStore"/>.
    /// </summary>
    /// <param name="contentStore">The content store every file name is looked up in.</param>
    public TftpProtocolServer(ContentStore contentStore)
    {
        ArgumentNullException.ThrowIfNull(contentStore);

        this.contentStore = contentStore;
    }

    /// <summary>
    /// The one scheme answered: <c>tftp</c>.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["tftp"]);

    /// <summary>
    /// Answers the request that opened <paramref name="flow"/>, from a new transfer port.
    /// </summary>
    /// <param name="flow">The flow opened by its first datagram.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(context);

        var first = flow.FirstDatagram;
        switch (TftpPacket.ReadOpcode(first.Span))
        {
            case TftpPacket.Error:
                context.Log.Note("The flow opened with an ERROR packet; it was not answered.");
                return;
            case TftpPacket.WriteRequest:
                await flow.MoveToNewLocalPortAsync(context.CancellationToken);
                await SendErrorAsync(flow, context, TftpErrorCode.AccessViolation, "Access violation", "A write request was refused: uploads are off");
                return;
        }

        await flow.MoveToNewLocalPortAsync(context.CancellationToken);
        if (!TftpReadRequest.TryParse(first.Span, out var request))
        {
            await SendErrorAsync(flow, context, TftpErrorCode.IllegalOperation, "Illegal TFTP operation", "The first datagram is not a well-formed read request");
            return;
        }

        await AnswerReadAsync(flow, context, request!);
    }

    private static async Task SendErrorAsync(IDatagramFlow flow, ExchangeContext context, TftpErrorCode errorCode, string message, string why)
    {
        context.Log.Note($"{why}; answered with ERROR {(int)errorCode}.");
        await flow.SendAsync(TftpPacket.ForError(errorCode, message), context.CancellationToken);
    }

    private async Task AnswerReadAsync(IDatagramFlow flow, ExchangeContext context, TftpReadRequest request)
    {
        var label = $"Read of \"{TftpLogText.Render(request.FileName)}\" ({request.Mode})";
        var mapping = contentStore.MapRequestPath(TftpFileName.ToRequestPath(request.FileName));
        var status = mapping.EntryKind == ContentEntryKind.File ? contentStore.GetFileStatus(mapping) : null;
        if (status is null)
        {
            var why = mapping.IsMapped ? $"no file at {mapping.Location}" : $"refused by the content store ({mapping.Refusal})";
            await SendErrorAsync(flow, context, TftpErrorCode.FileNotFound, "File not found", $"{label}: {why}");
            return;
        }

        var negotiation = TftpNegotiation.ForRead(request.Options, status.Length);
        context.Log.Note($"{label}: {status.Length} bytes of {mapping.Location} in blocks of {negotiation.BlockSize}");
        await new TftpReadTransfer(flow, context, contentStore, mapping, status.Length, negotiation).RunAsync();
    }
}
