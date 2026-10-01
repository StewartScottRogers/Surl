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
    /// Ends the connection's session, if it holds one, as the connection closes.
    /// </summary>
    public void EndSessionAsTheConnectionCloses()
    {
        if (session is not null)
        {
            EndSession("connection closed");
        }
    }

    // A Session field the request carries must name the connection's live session, whose
    // timeout it then restarts and which the answer names back; a request with none passes.
    private bool AcceptsTheNamedSession(HttpRequestHead head)
    {
        var namedId = head.GetFieldValues("Session").Select(value => value.Split(';')[0].Trim()).FirstOrDefault();
        if (namedId is null)
        {
            return true;
        }

        var held = LiveSession();
        if (held is null || !string.Equals(held.Id, namedId, StringComparison.Ordinal))
        {
            return false;
        }

        held.LastActivity = context.TimeProvider.GetUtcNow();
        sessionField = held.Id;

        return true;
    }

    // The connection's session, ended first if it has timed out.
    private RtspSession? LiveSession()
    {
        if (session is not null && session.HasTimedOut(context.TimeProvider.GetUtcNow()))
        {
            EndSession("timeout");
        }

        return session;
    }

    private void EndSession(string why)
    {
        context.Log.Note($"RTSP session {session!.Id} ended: {why}");
        session.End();
        session = null;
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

        return transport.Records
            ? RefuseAsync(RtspStatus.NotImplemented, cseq, head.Method, "recording is not served yet")
            : SetUpPresentationAsync(head, cseq, named, transport);
    }

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
            return LiveSession() is null ? null : (RtspStatus.MethodNotValidInThisState, "the connection already holds a session");
        }

        return named.IsPlaying ? (RtspStatus.MethodNotValidInThisState, "the session is playing") : null;
    }

    // A first SETUP makes the session; a later one re-negotiates the transport of the session's
    // own presentation.
    private Task<bool> SetUpPresentationAsync(HttpRequestHead head, string cseq, RtspSession? named, RtspTransport transport)
    {
        var path = PresentationPath(head.RequestTarget);
        var mapping = contentStore.MapRequestPath(path);
        if (PresentationFileStatus(mapping, out var whyNotFound) is null)
        {
            return RefuseAsync(RtspStatus.NotFound, cseq, head.Method, whyNotFound!);
        }

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

    private RtspSession NewSession(ContentPathMapping mapping, string path, RtspTransport transport)
    {
        var bytes = new byte[SessionRandomBytes];
        random.GetBytes(bytes);
        var span = bytes.AsSpan();
        session = new RtspSession(
            Convert.ToHexString(span[..8]),
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

        if (!head.GetFieldValues("Range").All(range => range.Trim() is "npt=0-" or "npt=now-"))
        {
            return RefuseAsync(RtspStatus.InvalidRange, cseq, head.Method, "only npt=0- and npt=now- are served");
        }

        var status = PresentationFileStatus(named!.Presentation, out var whyNotFound);
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

        return RespondAsync(ResponseHead(RtspStatus.Ok, cseq), null);
    }

    // TEARDOWN: ends the session; the answer names none, and the connection stays open.
    private Task<bool> TeardownAsync(HttpRequestHead head, string cseq)
    {
        var refusal = RefuseWithoutTheSessionsPresentation(head, cseq, NamedSession());
        if (refusal is not null)
        {
            return refusal;
        }

        EndSession("TEARDOWN");
        sessionField = null;

        return RespondAsync(ResponseHead(RtspStatus.Ok, cseq), null);
    }

    // PLAY, PAUSE and TEARDOWN need a session, and a Request-URI of * or the session's own
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
