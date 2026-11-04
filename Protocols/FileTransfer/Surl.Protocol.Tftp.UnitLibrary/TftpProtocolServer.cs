using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// The TFTP server (RFC 1350, with options from RFC 2347 to RFC 2349): answers the request
/// that opened a datagram flow from a new transfer port, serving reads from the content
/// store and writing uploads to it when uploads are allowed. ADR-0013 and ADR-0006 record
/// the answers below.
/// </summary>
/// <remarks>
/// <para>
/// One flow is one connection (ADR-0006 section 1): the server serves a whole transfer,
/// read or write, inside one <see cref="ServeAsync"/> on the one flow it is given, and never
/// opens another. Every reply leaves from a new local port, the server's transfer identifier
/// (RFC 1350 section 4): the flow is moved to it with
/// <see cref="IDatagramFlow.MoveToNewLocalPortAsync"/> before the first reply.
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
/// A write request (<c>WRQ</c>, <c>curl -T</c>) names the file the same way. It is answered
/// with ERROR 2, <c>Access violation</c>, and no file is created, when uploads are off
/// (<c>--allow-uploads</c>, ADR-0006 section 2), when the content store refuses the name, or
/// when the store does not permit an upload there (a hidden name, a directory, or no
/// directory to hold it); upstream curl exits 69. A <c>tsize</c> option past the exchange's
/// <see cref="ExchangeLimits.MaxUploadBytes"/> is answered with ERROR 3,
/// <c>Disk full or allocation exceeded</c>, before any DATA; upstream curl exits 70.
/// Otherwise the server opens the transfer with the OACK (the same options as a read, with
/// <c>tsize</c> echoed) or ACK 0, acknowledges each DATA block once the store has taken it,
/// and acknowledges the last block, the first shorter than the block size, once the whole
/// upload is written. An upload that grows past the store's upload limit, or that the store
/// fails to write (an <see cref="IOException"/>, such as a full disk), is answered with
/// ERROR 3 in place of the ACK, and the store deletes the partial file. The <c>tsize</c>
/// check comes before the store's own "not permitted" check, so a WRQ to a location the
/// store would not permit with a <c>tsize</c> past the limit gets ERROR 3.
/// </para>
/// <para>
/// A packet is sent again when the client's answer does not arrive within the
/// retransmission timeout: the negotiated <c>timeout</c>, or 5 seconds without one, on the
/// exchange's <see cref="ExchangeContext.TimeProvider"/>. After 5 retransmissions the
/// transfer is abandoned with no further packet. An answer of another block, such as a
/// duplicate ACK or DATA of the block before, is ignored and never triggers a resend. An
/// ERROR from the client ends the transfer; any other packet ends it with ERROR 4,
/// <c>Illegal TFTP operation</c>. A write that ends any of these ways leaves no file. If a
/// file being read shrinks while it is sent, the transfer ends with ERROR 0,
/// <c>The file changed while it was sent.</c>
/// </para>
/// <para>
/// A file name the content store refuses, one where nothing exists, one that names a
/// directory, and one that vanishes before it is read, are all answered with ERROR 1,
/// <c>File not found</c>, so a client cannot tell them apart (ADR-0006 section 2); upstream
/// curl exits 68. A first datagram that is an ERROR gets no reply; any other first datagram
/// that is not a well-formed read or write request (no final zero byte, an option without a
/// value, or a mode other than <c>octet</c> or <c>netascii</c>) is answered with ERROR 4,
/// <c>Illegal TFTP operation</c>.
/// </para>
/// <para>
/// A flow past a connection limit is answered, through <see cref="IDatagramRefusalWriter"/>,
/// with one ERROR 0 from a new transfer port: <c>Too many connections</c>, or
/// <c>Too many connections from your address</c> (ADR-0006 section 5); upstream curl exits
/// 71. Every ERROR message is one of the fixed texts above, never a path, an exception
/// message or an operating-system error (ADR-0006 section 3).
/// </para>
/// </remarks>
public sealed class TftpProtocolServer : IDatagramProtocolServer, IDatagramRefusalWriter
{
    /// <summary>The message of ERROR 2.</summary>
    internal const string AccessViolationMessage = "Access violation";

    /// <summary>The message of ERROR 3.</summary>
    internal const string DiskFullMessage = "Disk full or allocation exceeded";

    private readonly ContentStore contentStore;

    /// <summary>
    /// Creates a TFTP server that serves <paramref name="contentStore"/>.
    /// </summary>
    /// <param name="contentStore">The content store every file name is looked up in, and every upload written to.</param>
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
    /// Answers the request that opened <paramref name="flow"/>, from a new transfer port, and
    /// serves the whole transfer on that one flow.
    /// </summary>
    /// <param name="flow">The flow opened by its first datagram.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(context);

        var first = flow.FirstDatagram;
        if (TftpPacket.ReadOpcode(first.Span) == TftpPacket.Error)
        {
            context.Log.Note("The flow opened with an ERROR packet; it was not answered.");
            return;
        }

        await flow.MoveToNewLocalPortAsync(context.CancellationToken);
        if (!TftpRequest.TryParse(first.Span, out var request))
        {
            await SendErrorAsync(flow, context, TftpErrorCode.IllegalOperation, "Illegal TFTP operation", "The first datagram is not a well-formed read or write request");
            return;
        }

        var label = $"{request!.Kind} of \"{TftpLogText.Render(request.FileName)}\" ({request.Mode})";
        var mapping = contentStore.MapRequestPath(TftpFileName.ToRequestPath(request.FileName));
        await (request.IsWrite ? AnswerWriteAsync(flow, context, request, mapping, label) : AnswerReadAsync(flow, context, request, mapping, label));
    }

    /// <summary>
    /// Answers a flow past a connection limit with one ERROR 0 from a new transfer port:
    /// <c>Too many connections</c>, or <c>Too many connections from your address</c>.
    /// </summary>
    /// <param name="flow">The flow past the limit.</param>
    /// <param name="refusal">Which limit it is past.</param>
    /// <param name="cancellationToken">Cancelled when the engine gives up on the refusal.</param>
    /// <returns>A task that completes when the ERROR is sent.</returns>
    public async ValueTask WriteRefusalAsync(IDatagramFlow flow, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var message = refusal == ConnectionRefusal.TooManyConnectionsFromAddress
            ? "Too many connections from your address"
            : "Too many connections";
        await flow.MoveToNewLocalPortAsync(cancellationToken);
        await flow.SendAsync(TftpPacket.ForError(TftpErrorCode.NotDefined, message), cancellationToken);
    }

    private static async Task SendErrorAsync(IDatagramFlow flow, ExchangeContext context, TftpErrorCode errorCode, string message, string why)
    {
        context.Log.Note($"{why}; answered with ERROR {(int)errorCode}.");
        await flow.SendAsync(TftpPacket.ForError(errorCode, message), context.CancellationToken);
    }

    private async Task AnswerReadAsync(IDatagramFlow flow, ExchangeContext context, TftpRequest request, ContentPathMapping mapping, string label)
    {
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

    private async Task AnswerWriteAsync(IDatagramFlow flow, ExchangeContext context, TftpRequest request, ContentPathMapping mapping, string label)
    {
        if (WhyTheWriteIsRefused(mapping) is { } why)
        {
            await SendErrorAsync(flow, context, TftpErrorCode.AccessViolation, AccessViolationMessage, $"{label}: {why}");
            return;
        }

        var negotiation = TftpNegotiation.ForWrite(request.Options);
        var limit = context.Limits.MaxUploadBytes;
        if (limit != 0 && negotiation.TransferSize > limit)
        {
            await SendErrorAsync(flow, context, TftpErrorCode.DiskFull, DiskFullMessage, $"{label}: the announced {negotiation.TransferSize}-byte upload is past the upload limit of {limit} bytes");
            return;
        }

        context.Log.Note($"{label}: receiving into {mapping.Location} in blocks of {negotiation.BlockSize}");
        await new TftpWriteTransfer(flow, context, contentStore, mapping, negotiation, label).RunAsync();
    }

    private string? WhyTheWriteIsRefused(ContentPathMapping mapping) =>
        !contentStore.ExposureOptions.AllowUploads ? "refused, because uploads are off"
        : !mapping.IsMapped ? $"refused by the content store ({mapping.Refusal})"
        : null;
}
