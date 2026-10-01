using System.Buffers.Binary;
using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// The upload half of <see cref="RtspRequestResponder"/>: <c>ANNOUNCE</c>, which stores its
/// body as the presentation's description, <c>RECORD</c>, and the interleaved frames a client
/// sends, whose RTP payloads a recording session appends (ADR-0074 decision 6).
/// </summary>
internal sealed partial class RtspRequestResponder
{
    // The suffix an ANNOUNCE's description is stored under, beside the media it describes.
    private const string DescriptionSuffix = ".sdp";

    // A fresh upload, replacing whatever file is at the location once committed.
    private static readonly ContentUploadOpening ReplacingUpload = new(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false);

    // The opening results an upload is refused with; every one not named is 403, one answer for
    // uploads off, a hidden path and a directory (ADR-0015). Exists and Absent cannot happen
    // with ReplacingUpload.
    private static readonly Dictionary<ContentUploadOpeningResult, HttpStatus> UploadRefusalStatuses = new()
    {
        [ContentUploadOpeningResult.NoSuchDirectory] = RtspStatus.NotFound,
    };

    private readonly byte[] frameHeader = new byte[RtspInterleavedFrame.FrameHeaderBytes];

    // A frame's packet: at most the 16-bit length its header can give.
    private readonly byte[] framePacket = new byte[ushort.MaxValue];

    // The upload an ANNOUNCE's body is read into, opened before the body is read; null for any
    // other request, and for an ANNOUNCE that names no description location or will be refused.
    private ContentUploadOpeningOutcome? announcement;

    /// <summary>
    /// Reads one interleaved frame the client sent (RFC 2326 section 10.12): appends its RTP
    /// payload when it arrives on a recording session's RTP channel, and discards it otherwise.
    /// Called when the next byte on the connection is <see cref="RtspInterleavedFrame.Marker"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the connection stays open; <see langword="false"/> when the
    /// client closed in the middle of the frame, or the recording grew past the upload limit and
    /// the connection is closed (ADR-0074 decision 6).
    /// </returns>
    public async Task<bool> ReceiveInterleavedFrameAsync()
    {
        if (!await ReadExactlyAsync(frameHeader))
        {
            return ClientClosedInAFrame();
        }

        var packet = framePacket.AsMemory(0, BinaryPrimitives.ReadUInt16BigEndian(frameHeader.AsSpan(2)));
        if (!await ReadExactlyAsync(packet))
        {
            return ClientClosedInAFrame();
        }

        var recording = session is { IsRecording: true } recorder && frameHeader[1] == recorder.Transport.RtpChannel ? recorder.Recording : null;
        var payload = recording is null ? null : RtspInterleavedFrame.RtpPayload(packet.Span);
        if (payload is null || await recording!.WriteAtAsync(recording.Length, packet[payload.Value], context.CancellationToken) == ContentUploadResult.Written)
        {
            return true;
        }

        await EndSessionAsync($"the recording grew past the upload limit of {contentStore.ExposureOptions.MaxUploadBytes} bytes");

        return await CloseAfterAsync([], "close");
    }

    private bool ClientClosedInAFrame()
    {
        context.Log.Note("The client closed the connection in the middle of an interleaved frame.");

        return false;
    }

    // False when the client half-closed before the destination was filled.
    private async Task<bool> ReadExactlyAsync(Memory<byte> destination)
    {
        var filled = 0;
        while (filled < destination.Length)
        {
            var read = await reader.ReadAsync(destination[filled..], context.CancellationToken);
            if (read == 0)
            {
                return false;
            }

            filled += read;
        }

        return true;
    }

    // An ANNOUNCE the login lets in, with a body and an rtsp:// Request-URI, has its body read
    // into a fresh upload of <path>.sdp; whether it is kept is decided once the body is read.
    private async Task<ContentUploadOpeningOutcome?> OpenAnnouncementAsync(HttpRequestHead head, HttpAuthenticationVerdict verdict)
    {
        if (head.Method != "ANNOUNCE" || verdict.Outcome != HttpAuthenticationOutcome.Proceed || requestBodyLength == 0 || !head.RequestTarget.StartsWith(AbsoluteUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var mapping = contentStore.MapRequestPath(PresentationPath(head.RequestTarget) + DescriptionSuffix);

        return mapping.IsMapped ? await contentStore.OpenUploadAsync(mapping, ReplacingUpload, context.CancellationToken) : null;
    }

    // ANNOUNCE <path>: the body, byte for byte, stored as <path>.sdp (ADR-0074 decision 6).
    private async Task<bool> AnnounceAsync(HttpRequestHead head, string cseq)
    {
        if (head.RequestTarget == "*")
        {
            return await RefuseAsync(RtspStatus.BadRequest, cseq, head.Method, "* names no presentation");
        }

        if (requestBodyLength == 0)
        {
            return await RefuseAsync(RtspStatus.BadRequest, cseq, head.Method, "ANNOUNCE needs a description in its body");
        }

        var path = PresentationPath(head.RequestTarget) + DescriptionSuffix;

        return announcement?.Session is { } description
            ? await StoreDescriptionAsync(cseq, head.Method, path, description)
            : await RefuseUploadAsync(cseq, head.Method, path, announcement?.Result);
    }

    // The description read whole is committed over <path>.sdp: 200 once stored, 403 when the
    // store will not take it after all.
    private async Task<bool> StoreDescriptionAsync(string cseq, string method, string path, ContentUploadSession description)
    {
        var length = description.Length;
        var stored = await description.CommitAsync(context.CancellationToken);
        if (stored != ContentUploadResult.Written)
        {
            return await RefuseAsync(RtspStatus.Forbidden, cseq, method, $"{path} could not be stored ({stored})");
        }

        context.Log.Note($"Stored {length} bytes at {path}");

        return await RespondAsync(ResponseHead(RtspStatus.Ok, cseq), null);
    }

    // RECORD: the first opens the recording's upload; a later one resumes appending after a PAUSE.
    private async Task<bool> RecordAsync(HttpRequestHead head, string cseq)
    {
        var named = NamedSession();
        var refusal = RefuseWithoutTheSessionsPresentation(head, cseq, named);
        if (refusal is not null)
        {
            return await refusal;
        }

        if (!named!.Transport.Records)
        {
            return await RefuseAsync(RtspStatus.MethodNotValidInThisState, cseq, head.Method, "the session was set up to play");
        }

        if (named.Recording is null)
        {
            var opened = await contentStore.OpenUploadAsync(named.Presentation, ReplacingUpload, context.CancellationToken);
            if (opened.Session is null)
            {
                return await RefuseUploadAsync(cseq, head.Method, named.PresentationPath, opened.Result);
            }

            named.Recording = opened.Session;
        }

        named.IsRecording = true;

        return await RespondAsync(ResponseHead(RtspStatus.Ok, cseq), null);
    }

    // TEARDOWN of a recording session commits what it recorded; false when the location can no
    // longer take a file, and the recording is then gone.
    private async Task<bool> CommitRecordingAsync(RtspSession recorder)
    {
        var recording = recorder.Recording!;
        recorder.Recording = null;
        var length = recording.Length;
        var committed = await recording.CommitAsync(context.CancellationToken) == ContentUploadResult.Written;
        context.Log.Note(committed
            ? $"Stored {length} bytes at {recorder.PresentationPath}"
            : $"Recording of {recorder.PresentationPath} discarded: its location can no longer take a file");

        return committed;
    }

    // The store's refusals of an upload: 404 where the directory it belongs in does not exist,
    // 403 for everything else - uploads off, a refused or hidden path, a directory.
    private Task<bool> RefuseUploadAsync(string cseq, string method, string path, ContentUploadOpeningResult? result)
    {
        var status = result is { } refused ? UploadRefusalStatuses.GetValueOrDefault(refused, RtspStatus.Forbidden) : RtspStatus.Forbidden;

        return RefuseAsync(status, cseq, method, $"the content store refused an upload to {path} ({result?.ToString() ?? "a refused path"})");
    }
}
