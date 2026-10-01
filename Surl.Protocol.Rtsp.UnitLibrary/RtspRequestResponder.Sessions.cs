using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpMessage;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// The session half of <see cref="RtspRequestResponder"/>: <c>SETUP</c>, <c>PLAY</c>,
/// <c>PAUSE</c>, <c>TEARDOWN</c>, <c>GET_PARAMETER</c> and <c>SET_PARAMETER</c>, and the
/// interleaved RTP a playing session streams between requests (ADR-0074 decision 5).
/// </summary>
internal sealed partial class RtspRequestResponder
{
    // A session ID's 8 bytes, then the SSRC's 4, the first sequence number's 2 and the first
    // timestamp's 4, all drawn from the random source when a session is set up.
    private const int SessionRandomBytes = 8 + 4 + 2 + 4;

    private readonly RandomNumberGenerator random;

    // The connection's one session; null before a SETUP and after it ends.
    private RtspSession? session;

    // The Session field of the response being written: the ID the request named when the
    // connection holds it, or the SETUP answer's ID and timeout; null for neither.
    private string? sessionField;

    // The ID of the session TEARDOWN last ended, which stays the connection's until another
    // session is set up, because libcurl goes on naming it (ADR-0074 Amendment 1, decision 5).
    private string? tornDownSessionId;

    // Whether the request being answered names the torn-down session, so a SETUP takes its ID.
    private bool namesTheTornDownSession;

    /// <summary>
    /// While the connection's session is playing, writes its RTP packets one after another
    /// until the client starts another request or the presentation ends, so a request is read
    /// and answered between two frames (RFC 2326 section 10.12). A client that half-closes is
    /// streamed the rest of the presentation. Returns at once when nothing is playing.
    /// </summary>
    /// <returns>A task that completes when a request's first byte has arrived or the client half-closed, or at once.</returns>
    public async Task StreamUntilARequestArrivesAsync()
    {
        if (session is not { IsPlaying: true } playing)
        {
            return;
        }

        var arrival = reader.WaitForBytesAsync(context.CancellationToken).AsTask();
        while (playing.IsPlaying && !HasARequestStartedOrTheReadFailed(arrival))
        {
            await playing.SendNextPacketAsync(connection, contentStore, context);
        }

        if (!playing.IsPlaying)
        {
            context.Log.Note($"Streamed {playing.BytesThisPlay} bytes of {playing.PresentationPath} in {playing.PacketsThisPlay} RTP packets");
        }

        await arrival;
    }

    // A client that half-closed sends no more requests but still reads: the presentation plays
    // to its end before the connection closes.
    private static bool HasARequestStartedOrTheReadFailed(Task<bool> arrival) =>
        arrival.IsCompleted && (!arrival.IsCompletedSuccessfully || arrival.Result);

    /// <summary>
    /// Ends the connection's session, if it holds one, as the connection closes, discarding a
    /// recording not committed (ADR-0074 decision 6).
    /// </summary>
    /// <returns>A task that completes when the session has ended.</returns>
    public async Task EndSessionAsTheConnectionClosesAsync()
    {
        if (session is not null)
        {
            await EndSessionAsync("connection closed");
        }
    }

    // A Session field the request carries must name the connection's live session, whose
    // timeout it then restarts and which the answer names back; a request with none passes, and
    // so does one naming the session TEARDOWN ended, judged as naming none.
    private bool AcceptsTheNamedSession(HttpRequestHead head)
    {
        var namedId = head.GetFieldValues("Session").Select(value => value.Split(';')[0].Trim()).FirstOrDefault();
        if (namedId is null)
        {
            return true;
        }

        if (session is null || !string.Equals(session.Id, namedId, StringComparison.Ordinal))
        {
            namesTheTornDownSession = string.Equals(tornDownSessionId, namedId, StringComparison.Ordinal);

            return namesTheTornDownSession;
        }

        session.LastActivity = context.TimeProvider.GetUtcNow();
        sessionField = session.Id;

        return true;
    }

    // A session neither playing nor recording that no request named for its timeout has ended;
    // checked as each request arrives, before it is judged.
    private async Task EndTheSessionIfTimedOutAsync()
    {
        if (session is not null && session.HasTimedOut(context.TimeProvider.GetUtcNow()))
        {
            await EndSessionAsync("timeout");
        }
    }

    // A recording still open when the session ends any way but TEARDOWN is discarded with it.
    private async Task EndSessionAsync(string why)
    {
        var ending = session!;
        session = null;
        context.Log.Note($"RTSP session {ending.Id} ended: {why}");
        if (ending.Recording is not null)
        {
            context.Log.Note($"Recording of {ending.PresentationPath} discarded: {why}");
        }

        await ending.EndAsync();
    }

    // The session the request named, which every request but SETUP's first needs.
    private RtspSession? NamedSession() => sessionField is null ? null : session;

    // SETUP: the request's own checks, then the presentation and the session.
    private Task<bool> SetupAsync(HttpRequestHead head, string cseq)
    {
        var named = NamedSession();
        var transportValues = head.GetFieldValues("Transport");
        if (WhySetupIsRefused(head.RequestTarget, named, transportValues.Count) is { } refused)
        {
            return RefuseAsync(refused.Status, cseq, head.Method, refused.Check);
        }

        var transport = RtspTransport.Choose(transportValues, out var whyUnsupported);
        if (transport is null)
        {
            return RefuseAsync(RtspStatus.UnsupportedTransport, cseq, head.Method, whyUnsupported!);
        }

        return SetUpPresentationToPlayOrRecordAsync(head, cseq, named, transport);
    }

    // A session keeps the mode its first SETUP gave it; the presentation is then checked for
    // the mode the transport asks for.
    private Task<bool> SetUpPresentationToPlayOrRecordAsync(HttpRequestHead head, string cseq, RtspSession? named, RtspTransport transport)
    {
        if (named is not null && named.Transport.Records != transport.Records)
        {
            return RefuseAsync(RtspStatus.MethodNotValidInThisState, cseq, head.Method, "a SETUP cannot change whether the session plays or records");
        }

        var path = PresentationPath(head.RequestTarget);
        var mapping = contentStore.MapRequestPath(path);
        var presentationRefusal = transport.Records ? WhyRecordingIsRefused(mapping) : WhyPlayingIsRefused(mapping);

        return presentationRefusal is { } refusedPresentation
            ? RefuseAsync(refusedPresentation.Status, cseq, head.Method, refusedPresentation.Check)
            : SetUpPresentationAsync(head, cseq, named, transport, path, mapping);
    }

    // A presentation to play is a file in the content store (ADR-0074 decision 4).
    private (HttpStatus Status, string Check)? WhyPlayingIsRefused(ContentPathMapping mapping) =>
        PresentationFileStatus(mapping, out var whyNotFound) is null ? (RtspStatus.NotFound, whyNotFound!) : null;

    // A presentation to record need not exist, but a SETUP to record is a write: refused without
    // --allow-uploads before a session is made, and for a path the store refuses (decision 6).
    private (HttpStatus Status, string Check)? WhyRecordingIsRefused(ContentPathMapping mapping) =>
        !contentStore.ExposureOptions.AllowUploads ? (RtspStatus.Forbidden, "uploads are not allowed")
        : !mapping.IsMapped ? (RtspStatus.Forbidden, $"the path was refused ({mapping.Refusal})")
        : null;

    // SETUP * names no presentation, a SETUP needs a Transport, a connection holds one session,
    // and a playing session cannot be set up again; null when none of those refuses it.
    private (HttpStatus Status, string Check)? WhySetupIsRefused(string requestTarget, RtspSession? named, int transportCount) =>
        requestTarget == "*" ? (RtspStatus.BadRequest, "* names no presentation")
        : transportCount == 0 ? (RtspStatus.BadRequest, "SETUP needs a Transport")
        : WhyTheSessionStateRefusesSetup(named);

    private (HttpStatus Status, string Check)? WhyTheSessionStateRefusesSetup(RtspSession? named)
    {
        if (named is null)
        {
            return session is null ? null : (RtspStatus.MethodNotValidInThisState, "the connection already holds a session");
        }

        return named.IsPlaying ? (RtspStatus.MethodNotValidInThisState, "the session is playing")
            : named.IsRecording ? (RtspStatus.MethodNotValidInThisState, "the session is recording")
            : null;
    }

    // A first SETUP makes the session; a later one re-negotiates the transport of the session's
    // own presentation.
    private Task<bool> SetUpPresentationAsync(HttpRequestHead head, string cseq, RtspSession? named, RtspTransport transport, string path, ContentPathMapping mapping)
    {
        if (named is not null && !string.Equals(named.Presentation.Location, mapping.Location, StringComparison.Ordinal))
        {
            return RefuseAsync(RtspStatus.MethodNotValidInThisState, cseq, head.Method, "the session holds another presentation");
        }

        var setUp = named ?? NewSession(mapping, path, transport);
        setUp.Transport = transport;
        sessionField = string.Create(CultureInfo.InvariantCulture, $"{setUp.Id};timeout={RtspSession.Timeout.TotalSeconds}");
        context.Log.Note($"RTSP session {setUp.Id} set up for {path}, interleaved {transport.RtpChannel}-{transport.RtcpChannel}");

        return RespondAsync(ResponseHead(RtspStatus.Ok, cseq).AddField("Transport", transport.Describe(setUp.Ssrc)), null);
    }

    // A SETUP naming the torn-down session makes the new one under its ID, which libcurl
    // compares, failing 86 on any other (ADR-0074 Amendment 1).
    private RtspSession NewSession(ContentPathMapping mapping, string path, RtspTransport transport)
    {
        var bytes = new byte[SessionRandomBytes];
        random.GetBytes(bytes);
        var span = bytes.AsSpan();
        var id = namesTheTornDownSession ? tornDownSessionId! : Convert.ToHexString(span[..8]);
        tornDownSessionId = null;
        session = new RtspSession(
            id,
            mapping,
            path,
            transport,
            BinaryPrimitives.ReadUInt32BigEndian(span[8..]),
            BinaryPrimitives.ReadUInt16BigEndian(span[12..]),
            BinaryPrimitives.ReadUInt32BigEndian(span[14..]),
            context.TimeProvider.GetUtcNow());

        return session;
    }

    // PLAY: from the start, or on from a PAUSE; the frames follow the answer, between requests.
    private Task<bool> PlayAsync(HttpRequestHead head, string cseq)
    {
        var named = NamedSession();
        var refusal = RefuseWithoutTheSessionsPresentation(head, cseq, named);
        if (refusal is not null)
        {
            return refusal;
        }

        if (named!.Transport.Records)
        {
            return RefuseAsync(RtspStatus.MethodNotValidInThisState, cseq, head.Method, "the session was set up to record");
        }

        if (!head.GetFieldValues("Range").All(range => range.Trim() is "npt=0-" or "npt=now-"))
        {
            return RefuseAsync(RtspStatus.InvalidRange, cseq, head.Method, "only npt=0- and npt=now- are served");
        }

        var status = PresentationFileStatus(named.Presentation, out var whyNotFound);
        if (status is null)
        {
            return RefuseAsync(RtspStatus.NotFound, cseq, head.Method, whyNotFound!);
        }

        var rtpInfo = string.Create(CultureInfo.InvariantCulture, $"url={head.RequestTarget};seq={named.NextSequenceNumber};rtptime={named.NextTimestamp}");
        named.Play(status.Length);

        return RespondAsync(ResponseHead(RtspStatus.Ok, cseq).AddField("Range", "npt=0-").AddField("RTP-Info", rtpInfo), null);
    }

    // PAUSE: stops after the frame being written and keeps the position; in Ready, nothing to pause.
    private Task<bool> PauseAsync(HttpRequestHead head, string cseq)
    {
        var named = NamedSession();
        var refusal = RefuseWithoutTheSessionsPresentation(head, cseq, named);
        if (refusal is not null)
        {
            return refusal;
        }

        if (named!.IsPlaying)
        {
            context.Log.Note($"Streamed {named.BytesThisPlay} bytes of {named.PresentationPath}, paused after {named.PacketsThisPlay} packets");
            named.Pause();
        }

        named.IsRecording = false;

        return RespondAsync(ResponseHead(RtspStatus.Ok, cseq), null);
    }

    // TEARDOWN: commits a recording, then ends the session, whose ID the connection keeps; the
    // answer names none, and the connection stays open. A recording whose location can no
    // longer take a file is 403.
    private async Task<bool> TeardownAsync(HttpRequestHead head, string cseq)
    {
        var named = NamedSession();
        var refusal = RefuseWithoutTheSessionsPresentation(head, cseq, named);
        if (refusal is not null)
        {
            return await refusal;
        }

        var committed = named!.Recording is null || await CommitRecordingAsync(named);
        await EndSessionAsync("TEARDOWN");
        tornDownSessionId = named.Id;
        sessionField = null;

        return committed
            ? await RespondAsync(ResponseHead(RtspStatus.Ok, cseq), null)
            : await RefuseAsync(RtspStatus.Forbidden, cseq, head.Method, "the recording's location can no longer take a file");
    }

    // PLAY, PAUSE, RECORD and TEARDOWN need a session, and a Request-URI of * or the session's own
    // presentation; null when the request has both.
    private Task<bool>? RefuseWithoutTheSessionsPresentation(HttpRequestHead head, string cseq, RtspSession? named)
    {
        if (named is null)
        {
            return RefuseAsync(RtspStatus.SessionNotFound, cseq, head.Method, "the request names no session");
        }

        if (head.RequestTarget == "*")
        {
            return null;
        }

        var mapping = contentStore.MapRequestPath(PresentationPath(head.RequestTarget));

        return string.Equals(mapping.Location, named.Presentation.Location, StringComparison.Ordinal)
            ? null
            : RefuseAsync(RtspStatus.NotFound, cseq, head.Method, "the Request-URI names another presentation than the session's");
    }

    // GET_PARAMETER and SET_PARAMETER with no body are the keep-alive; surl has no parameter to
    // report or set, so a body naming one is not understood.
    private Task<bool> AnswerParameterRequestAsync(HttpRequestHead head, string cseq) =>
        requestBodyLength == 0
            ? RespondAsync(ResponseHead(RtspStatus.Ok, cseq), null)
            : RefuseAsync(RtspStatus.ParameterNotUnderstood, cseq, head.Method, "surl has no parameters to report or set");
}
