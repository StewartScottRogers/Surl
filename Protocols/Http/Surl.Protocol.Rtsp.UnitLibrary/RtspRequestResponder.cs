using System.Globalization;
using System.Security.Cryptography;
using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// Writes the response to each request read from one connection, as
/// <see cref="RtspProtocolServer"/> describes, and says whether the connection stays open.
/// </summary>
internal sealed partial class RtspRequestResponder
{
    /// <summary>
    /// How long a refusal that closes the connection may take to write before the server gives
    /// up on it (ADR-0006, section 5; ADR-0019).
    /// </summary>
    public static readonly TimeSpan RefusalWriteDeadline = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The <c>Public</c> field of every <c>OPTIONS</c> answer: all ten methods RFC 2326 and
    /// upstream libcurl's <c>CURLOPT_RTSP_REQUEST</c> name (ADR-0074 decision 3).
    /// </summary>
    public const string PublicMethods = "OPTIONS, DESCRIBE, ANNOUNCE, SETUP, PLAY, PAUSE, TEARDOWN, GET_PARAMETER, SET_PARAMETER, RECORD";

    private const int MaxCSeqDigits = 9;

    private const string AbsoluteUrlPrefix = "rtsp://";

    private const int BodyBufferBytes = 8192;

    // How each of the ten methods is answered once decision 2's checks have passed.
    private static readonly Dictionary<string, Func<RtspRequestResponder, HttpRequestHead, string, Task<bool>>> MethodAnswers = new(StringComparer.Ordinal)
    {
        ["OPTIONS"] = (responder, _, cseq) => responder.RespondAsync(responder.ResponseHead(RtspStatus.Ok, cseq).AddField("Public", PublicMethods), null),
        ["DESCRIBE"] = (responder, head, cseq) => responder.DescribeAsync(head, cseq),
        ["ANNOUNCE"] = (responder, head, cseq) => responder.AnnounceAsync(head, cseq),
        ["RECORD"] = (responder, head, cseq) => responder.RecordAsync(head, cseq),
        ["SETUP"] = (responder, head, cseq) => responder.SetupAsync(head, cseq),
        ["PLAY"] = (responder, head, cseq) => responder.PlayAsync(head, cseq),
        ["PAUSE"] = (responder, head, cseq) => responder.PauseAsync(head, cseq),
        ["TEARDOWN"] = (responder, head, cseq) => responder.TeardownAsync(head, cseq),
        ["GET_PARAMETER"] = (responder, head, cseq) => responder.AnswerParameterRequestAsync(head, cseq),
        ["SET_PARAMETER"] = (responder, head, cseq) => responder.AnswerParameterRequestAsync(head, cseq),
    };

    // Every other failure to read a head is a malformed head: 400.
    private static readonly Dictionary<HttpRequestHeadReadOutcome, HttpStatus> StatusesForHeadsNotRead = new()
    {
        [HttpRequestHeadReadOutcome.UnsupportedVersion] = RtspStatus.RtspVersionNotSupported,
        [HttpRequestHeadReadOutcome.HeadTooLarge] = RtspStatus.RequestHeaderFieldsTooLarge,
        [HttpRequestHeadReadOutcome.HeadTimedOut] = RtspStatus.RequestTimeOut,
    };

    private readonly IConnection connection;
    private readonly HttpConnectionReader reader;
    private readonly ExchangeContext context;
    private readonly ContentStore contentStore;
    private readonly RtspUnreadRequestDrainer unreadRequestDrainer;
    private readonly IHttpAuthenticationSession authenticationSession;
    private readonly byte[] bodyBuffer = new byte[BodyBufferBytes];

    // The WWW-Authenticate values the authentication session gave for the request being
    // answered, written on whatever response it gets; none before a request is judged.
    private IReadOnlyList<string> wwwAuthenticateValues = [];

    // The Content-Length of the request being answered; 0 when it has no body.
    private long requestBodyLength;

    /// <summary>
    /// Creates a responder for one connection.
    /// </summary>
    /// <param name="connection">Where responses are written.</param>
    /// <param name="reader">The connection's reader, through which request bodies are read.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <param name="contentStore">Where presentation paths are looked up.</param>
    /// <param name="authenticationSession">The connection's authentication session, which judges every request.</param>
    /// <param name="random">The random source of session IDs, SSRCs, and first sequence numbers and timestamps.</param>
    public RtspRequestResponder(IConnection connection, HttpConnectionReader reader, ExchangeContext context, ContentStore contentStore, IHttpAuthenticationSession authenticationSession, RandomNumberGenerator random)
    {
        this.connection = connection;
        this.reader = reader;
        this.context = context;
        this.contentStore = contentStore;
        this.authenticationSession = authenticationSession;
        this.random = random;
        unreadRequestDrainer = new RtspUnreadRequestDrainer(reader, context);
    }

    /// <summary>
    /// Answers a connection on which no request head could be read: nothing when the client
    /// closed it or never sent a byte before the head timeout, otherwise a refusal with no
    /// <c>CSeq</c> that closes it (ADR-0074 decision 2, check 1).
    /// </summary>
    /// <param name="outcome">Why no head was read.</param>
    /// <returns><see langword="false"/>: the connection never stays open after this.</returns>
    public Task<bool> AnswerHeadNotReadAsync(HttpRequestHeadReadOutcome outcome)
    {
        if (outcome is HttpRequestHeadReadOutcome.ConnectionClosed or HttpRequestHeadReadOutcome.ConnectionClosedBeforeHeadEnded)
        {
            context.Log.Note($"The client closed the connection: {outcome}.");

            return Task.FromResult(false);
        }

        if (outcome is HttpRequestHeadReadOutcome.HeadTimedOutBeforeAnyByte)
        {
            context.Log.Note($"No request head was read: {outcome}; closed with no bytes.");

            return Task.FromResult(false);
        }

        var status = StatusesForHeadsNotRead.GetValueOrDefault(outcome, RtspStatus.BadRequest);

        return RefuseAndCloseAsync(status, null, "request", $"no request head was read ({outcome})");
    }

    /// <summary>
    /// Answers a request whose head was read, judging it in ADR-0074 decision 2's order: its
    /// <c>CSeq</c>, its body framing, its login, its method, its Request-URI, then the method's
    /// own checks.
    /// </summary>
    /// <param name="head">The request head.</param>
    /// <returns><see langword="true"/> when the connection stays open for another request.</returns>
    public async Task<bool> AnswerRequestAsync(HttpRequestHead head)
    {
        wwwAuthenticateValues = [];
        sessionField = null;
        namesTheTornDownSession = false;
        announcement = null;
        await EndTheSessionIfTimedOutAsync();
        var framing = HttpRequestBodyFraming.Of(head);
        requestBodyLength = framing.ContentLength;
        var cseq = SingleCSeq(head);
        if (cseq is null)
        {
            return await RefuseMissingCSeqAsync(head, framing);
        }

        var framingRefusal = RefuseBodyFramingAsync(head, cseq, framing);
        if (framingRefusal is not null)
        {
            return await framingRefusal;
        }

        var verdict = await authenticationSession.JudgeAsync(AuthenticationRequest(head), context.CancellationToken);
        using var bodyHash = verdict.BodyCheck is null ? null : IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        announcement = await OpenAnnouncementAsync(head, verdict);
        await using var announcedDescription = announcement?.Session;
        if (!await ReadBodyAsync(framing.ContentLength, bodyHash, announcedDescription))
        {
            return await RefuseAndCloseAsync(RtspStatus.BadRequest, cseq, head.Method, "the body ended before its Content-Length");
        }

        if (verdict.BodyCheck is not null)
        {
            verdict = await verdict.BodyCheck.JudgeBodyAsync(bodyHash!.GetHashAndReset(), context.CancellationToken);
        }

        return await AnswerVerdictAsync(head, cseq, verdict);
    }

    // What the authentication session is shown of a request: never its body (ADR-0032,
    // section 6). ANNOUNCE, RECORD and a SETUP for recording are writes (ADR-0074 decision 7).
    private static HttpAuthenticationRequest AuthenticationRequest(HttpRequestHead head) => new(
        head.Method,
        head.RequestTarget,
        IsWrite(head),
        head.Fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)).ToArray());

    private static bool IsWrite(HttpRequestHead head) => head.Method switch
    {
        "ANNOUNCE" or "RECORD" => true,
        "SETUP" => head.GetFieldValues("Transport").Any(AsksToRecord),
        _ => false,
    };

    // RFC 2326 section 12.39: a transport's mode parameter, mode=record or mode="RECORD", or a
    // quoted list naming it. Judging too many SETUPs as writes only asks for a login more often.
    private static bool AsksToRecord(string transport) =>
        transport.Split(';').Any(parameter =>
            parameter.TrimStart().StartsWith("mode=", StringComparison.OrdinalIgnoreCase)
            && parameter.Contains("record", StringComparison.OrdinalIgnoreCase));

    // Check 4 (ADR-0074 decision 7): the session's login note, then the request served, or
    // refused with a 401 carrying its challenges or a 403; neither closes the connection, so
    // curl answers Digest on it. The note never repeats the Authorization field.
    private Task<bool> AnswerVerdictAsync(HttpRequestHead head, string cseq, HttpAuthenticationVerdict verdict)
    {
        wwwAuthenticateValues = verdict.WwwAuthenticateValues;
        if (verdict.CheckedLogin is not null)
        {
            context.Log.Note(verdict.CheckedLogin.Note);
        }

        return verdict.Outcome switch
        {
            HttpAuthenticationOutcome.Proceed => AnswerMethodAsync(head, cseq),
            HttpAuthenticationOutcome.Challenge => RefuseAsync(RtspStatus.Unauthorized, cseq, head.Method, "a login is needed"),
            _ => RefuseAsync(RtspStatus.Forbidden, cseq, head.Method, "the authentication policy forbade the request"),
        };
    }

    // Check 2: exactly one CSeq of 1 to 9 decimal digits, copied byte for byte; null otherwise.
    private static string? SingleCSeq(HttpRequestHead head)
    {
        var values = head.GetFieldValues("CSeq");

        return values.Count == 1 && IsCSeqNumber(values[0]) ? values[0] : null;
    }

    private static bool IsCSeqNumber(string value) =>
        value.Length is > 0 and <= MaxCSeqDigits && value.All(char.IsAsciiDigit);

    // A request without a usable CSeq is answered 400 with no CSeq and the connection kept;
    // when it announced a body, which is never read, the connection cannot be kept in step,
    // so the 400 closes it.
    private Task<bool> RefuseMissingCSeqAsync(HttpRequestHead head, HttpRequestBodyFraming framing)
    {
        const string Check = "the request needs exactly one CSeq of 1 to 9 digits";

        return framing.Kind == HttpRequestBodyFramingKind.None
            ? RefuseAsync(RtspStatus.BadRequest, null, head.Method, Check)
            : RefuseAndCloseAsync(RtspStatus.BadRequest, null, head.Method, Check);
    }

    // Check 3: RFC 2326 has no chunked bodies, a Content-Length must be one field of digits,
    // and a body past the upload limit is refused before any of it is read; null when the
    // framing is one the server reads.
    private Task<bool>? RefuseBodyFramingAsync(HttpRequestHead head, string cseq, HttpRequestBodyFraming framing) => framing.Kind switch
    {
        HttpRequestBodyFramingKind.Chunked or HttpRequestBodyFramingKind.Unreadable =>
            RefuseAndCloseAsync(RtspStatus.BadRequest, cseq, head.Method, "RTSP requests carry no Transfer-Encoding"),
        HttpRequestBodyFramingKind.InvalidContentLength =>
            RefuseAndCloseAsync(RtspStatus.BadRequest, cseq, head.Method, "the Content-Length is not one field of decimal digits"),
        HttpRequestBodyFramingKind.ContentLength when IsPastUploadLimit(framing.ContentLength) =>
            RefuseAndCloseAsync(RtspStatus.RequestEntityTooLarge, cseq, head.Method, $"the {framing.ContentLength}-byte body is past the upload limit of {context.Limits.MaxUploadBytes} bytes"),
        _ => null,
    };

    private bool IsPastUploadLimit(long contentLength) =>
        context.Limits.MaxUploadBytes != 0 && contentLength > context.Limits.MaxUploadBytes;

    // The body is read before the answer, so the next head starts where the client expects
    // (ADR-0074 decision 2): an ANNOUNCE's into the upload of its description, every other
    // discarded, and hashed when a login is bound to it. False when the client closed before
    // all of it arrived.
    private async Task<bool> ReadBodyAsync(long contentLength, IncrementalHash? bodyHash, ContentUploadSession? upload)
    {
        var left = contentLength;
        while (left > 0)
        {
            var read = await reader.ReadAsync(bodyBuffer.AsMemory(0, (int)Math.Min(bodyBuffer.Length, left)), context.CancellationToken);
            if (read == 0)
            {
                return false;
            }

            bodyHash?.AppendData(bodyBuffer, 0, read);
            if (upload is not null)
            {
                await upload.WriteAtAsync(upload.Length, bodyBuffer.AsMemory(0, read), context.CancellationToken);
            }

            left -= read;
        }

        return true;
    }

    // Checks 5 to 7, a Session the request names being the connection's (ADR-0074 decision 5)
    // the first of the method's own.
    private Task<bool> AnswerMethodAsync(HttpRequestHead head, string cseq)
    {
        if (!MethodAnswers.TryGetValue(head.Method, out var answer))
        {
            return RefuseAsync(RtspStatus.NotImplemented, cseq, head.Method, "the method is not an RTSP method");
        }

        if (!IsRequestUri(head.RequestTarget))
        {
            return RefuseAsync(RtspStatus.BadRequest, cseq, head.Method, "the Request-URI is neither * nor an rtsp:// URL");
        }

        if (!AcceptsTheNamedSession(head))
        {
            return RefuseAsync(RtspStatus.SessionNotFound, cseq, head.Method, "the connection holds no session of that ID");
        }

        return answer(this, head, cseq);
    }

    private static bool IsRequestUri(string requestTarget) =>
        requestTarget == "*" || requestTarget.StartsWith(AbsoluteUrlPrefix, StringComparison.OrdinalIgnoreCase);

    // ADR-0074 decision 4: a file is described as one stream of its own bytes; *, a refused
    // path, a missing or hidden one, a directory and a file whose status cannot be read are not
    // presentations.
    private Task<bool> DescribeAsync(HttpRequestHead head, string cseq)
    {
        if (head.RequestTarget == "*")
        {
            return RefuseAsync(RtspStatus.BadRequest, cseq, head.Method, "* names no presentation");
        }

        var mapping = contentStore.MapRequestPath(PresentationPath(head.RequestTarget));
        if (PresentationFileStatus(mapping, out var whyNotFound) is null)
        {
            return RefuseAsync(RtspStatus.NotFound, cseq, head.Method, whyNotFound!);
        }

        var description = RtspSessionDescription.Describe(Path.GetFileName(mapping.Location!), connection.LocalEndPoint);
        var responseHead = ResponseHead(RtspStatus.Ok, cseq)
            .AddField("Content-Type", "application/sdp")
            .AddField("Content-Base", head.RequestTarget)
            .AddField("Content-Length", description.Length.ToString(CultureInfo.InvariantCulture));

        return RespondAsync(responseHead, description);
    }

    // The status of the file a presentation mapping names; null, with why, when the path was
    // refused or names no file. A file whose status cannot be read is answered as one that
    // does not exist (ADR-0023).
    private ContentFileStatus? PresentationFileStatus(ContentPathMapping mapping, out string? whyNotFound)
    {
        if (!mapping.IsMapped)
        {
            whyNotFound = $"the path was refused ({mapping.Refusal})";

            return null;
        }

        try
        {
            var status = contentStore.GetFileStatus(mapping);
            whyNotFound = status is null ? $"no file exists at {mapping.Location}" : null;

            return status;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            whyNotFound = $"{mapping.Location} could not be read ({failure.GetType().Name}: {failure.Message})";

            return null;
        }
    }

    // The path of an absolute rtsp:// Request-URI, without its query; "/" when it has none.
    private static string PresentationPath(string requestUri)
    {
        var pathStart = requestUri.IndexOfAny(['/', '?'], AbsoluteUrlPrefix.Length);
        var path = pathStart >= 0 && requestUri[pathStart] == '/' ? requestUri[pathStart..] : "/";
        var query = path.IndexOf('?', StringComparison.Ordinal);

        return query < 0 ? path : path[..query];
    }

    // ADR-0074 decision 1: CSeq first, copied from the request (none when it had no usable
    // one), then Date and Server, then the authentication session's WWW-Authenticate values,
    // then the Session the request named, when the connection holds it (decision 5).
    private HttpResponseHead ResponseHead(HttpStatus status, string? cseq) =>
        new HttpResponseHead(HttpMessageProtocol.Rtsp10, status)
            .AddField("CSeq", cseq)
            .AddField("Date", context.TimeProvider.GetUtcNow().ToUniversalTime().ToString("r", CultureInfo.InvariantCulture))
            .AddField("Server", HttpResponseHead.ServerName)
            .AddChallengeFields(wwwAuthenticateValues)
            .AddField("Session", sessionField);

    private async Task<bool> RespondAsync(HttpResponseHead responseHead, byte[]? body)
    {
        await connection.WriteAsync(responseHead.ToBytes(), context.CancellationToken);
        if (body is not null)
        {
            await connection.WriteAsync(body, context.CancellationToken);
        }

        return true;
    }

    // A refusal that keeps the connection: no body, no Content-Length (ADR-0074 decision 1).
    private Task<bool> RefuseAsync(HttpStatus status, string? cseq, string method, string check)
    {
        NoteRefusal(status, method, check);

        return RespondAsync(ResponseHead(status, cseq), null);
    }

    // A refusal that closes gets one second to be written and half-closed; past that the
    // server gives up on it and leaves the close to the engine, never an abort (ADR-0006,
    // section 5). Once it is written, what the client still sends is drained (ADR-0024).
    private async Task<bool> RefuseAndCloseAsync(HttpStatus status, string? cseq, string method, string check)
    {
        NoteRefusal(status, method, check);

        return await CloseAfterAsync(ResponseHead(status, cseq).ToBytes(), status.Code.ToString(CultureInfo.InvariantCulture));
    }

    // Writes the last bytes, half-closes and drains what the client still sends, within the
    // refusal deadline (ADR-0059's farewell window); a recording past the upload limit closes
    // this way with no bytes, there being no request to answer (ADR-0074 decision 6).
    private async Task<bool> CloseAfterAsync(byte[] lastBytes, string what)
    {
        if (await TryWriteAndHalfCloseAsync(lastBytes, what))
        {
            await unreadRequestDrainer.DrainAsync();
        }

        return false;
    }

    private async Task<bool> TryWriteAndHalfCloseAsync(byte[] lastBytes, string what)
    {
        using var deadline = new CancellationTokenSource(RefusalWriteDeadline, context.TimeProvider);
        using var deadlineOrExchange = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        try
        {
            await connection.WriteAsync(lastBytes, deadlineOrExchange.Token);
            await connection.CompleteWritesAsync(deadlineOrExchange.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note($"The {what} was not written within its {RefusalWriteDeadline.TotalSeconds}-second write deadline; the connection is closed without it.");

            return false;
        }

        return true;
    }

    private void NoteRefusal(HttpStatus status, string method, string check) =>
        context.Log.Note($"RTSP {method} refused: {status.Code} {status.ReasonPhrase}: {check}");
}
