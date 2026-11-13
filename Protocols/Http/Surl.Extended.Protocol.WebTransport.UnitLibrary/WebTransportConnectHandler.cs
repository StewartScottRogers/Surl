using Surl.Http3;
using Surl.Protocol.Abstractions;
using Surl.Qpack;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The extended <c>CONNECT</c> handler for <c>webtransport-h3</c> on one connection (ADR-0117
/// decisions 0, 1, 4, 5 and 7): it waits for the client's <c>SETTINGS</c>, judges the request's
/// origin, login, path and the session limit, answers <c>200</c> and serves the session with a
/// <see cref="WebTransportSession"/>, with flow control when the client's <c>SETTINGS</c> turn it on.
/// </summary>
/// <param name="acceptedPath">The listen URL's path; empty or <c>/</c> accepts every path.</param>
/// <param name="authentication">Judges each <c>CONNECT</c>'s login.</param>
/// <param name="flowControlSettings">The flow-control limits the server offers each session.</param>
/// <param name="directory">The connection's sessions, for the streams that name them.</param>
/// <param name="context">The exchange: its clock, limits and log.</param>
internal sealed class WebTransportConnectHandler(
    string acceptedPath,
    WebTransportConnectAuthentication authentication,
    WebTransportFlowControlSettings flowControlSettings,
    WebTransportSessionDirectory directory,
    ExchangeContext context) : IHttp3ExtendedConnectHandler
{
    /// <summary>The <c>:protocol</c> WTH3 3.2 names.</summary>
    public const string ProtocolName = "webtransport-h3";

    /// <summary>The sessions a connection may hold at once without flow control (WTH3 5.1; ADR-0117 decision 5).</summary>
    public const int SessionLimitWithoutFlowControl = 1;

    /// <summary>The sessions a connection may hold at once with flow control (WTH3 5.2; ADR-0117 decision 5).</summary>
    public const int SessionLimitWithFlowControl = 16;

    /// <inheritdoc />
    public string Protocol => ProtocolName;

    /// <inheritdoc />
    public ulong? BidirectionalStreamSignal => (ulong)WebTransportStreamKind.Bidirectional;

    /// <inheritdoc />
    public ulong? UnidirectionalStreamType => (ulong)WebTransportStreamKind.Unidirectional;

    /// <summary>The fields of an answer of <paramref name="status"/>: <c>allow: CONNECT</c> goes with a 405 (RFC 9110 section 15.5.6).</summary>
    /// <param name="status">The status.</param>
    /// <returns>The field lines.</returns>
    public static QpackFieldLine[] AnswerFields(int status) => status == WebTransportConnectJudgement.MethodNotAllowed
        ? [new(":status", "405"), new("allow", "CONNECT")]
        : [new(":status", status.ToString(System.Globalization.CultureInfo.InvariantCulture))];

    /// <inheritdoc />
    public async Task ServeSessionAsync(Http3ExtendedConnectSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var settings = await ReadClientSettingsAsync(session, cancellationToken);
        if (await RefuseAsync(session, settings, cancellationToken))
        {
            directory.Settle(session.Id, null);
            return;
        }

        try
        {
            using var served = new WebTransportSession(session, context, FlowControlFor(settings!));
            await session.RequestStream.WriteHeadersAsync(AnswerFields(WebTransportConnectJudgement.Accepted), endStream: false, cancellationToken);
            served.MarkAnswered();
            context.Log.Note($"WebTransport session {session.Id} on {session.Head.Target} was accepted.");
            directory.Settle(session.Id, served);
            await served.ServeAsync(cancellationToken);
        }
        finally
        {
            directory.End(session.Id);
            context.Log.Note($"WebTransport session {session.Id} has ended.");
        }
    }

    /// <inheritdoc />
    public Task ServeStreamWithoutSessionAsync(Http3SessionStream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return directory.ServeStreamAsync(stream, cancellationToken);
    }

    // Answers or resets a CONNECT the server does not accept, and says whether it did; an accepted one holds a place under the session limit.
    private async Task<bool> RefuseAsync(Http3ExtendedConnectSession session, IReadOnlyDictionary<ulong, ulong>? settings, CancellationToken cancellationToken)
    {
        if (settings is null)
        {
            await session.RequestStream.ResetAsync(Http3ErrorCode.RequestIncomplete, "the client's SETTINGS did not arrive within the head timeout", cancellationToken);
            return true;
        }

        if (!MeetsRequirements(settings))
        {
            // WTH3 3.1: the client cannot use WebTransport at all, so the connection closes, not just the request.
            session.CloseConnection((Http3ErrorCode)WebTransportHttp3Codes.RequirementsNotMet, "the client's SETTINGS lack SETTINGS_H3_DATAGRAM = 1 or SETTINGS_WT_ENABLED = 1");
            return true;
        }

        if (await JudgeAsync(session.Head, cancellationToken) is { } answer)
        {
            context.Log.Note($"WebTransport CONNECT {session.Id} to {session.Head.Target} was answered {answer[0].Value}.");
            await session.RequestStream.WriteHeadersAsync(answer, endStream: true, cancellationToken);
            return true;
        }

        if (await WebTransportConnectFaults.FindAsync(context, session.Head.Target) is { } chosen)
        {
            // ADR-0124 decision 9: the chosen status, with the rule's text as a plain-text body.
            await session.RequestStream.WriteHeadersAsync([.. AnswerFields(chosen.Status), new("content-type", "text/plain; charset=utf-8")], endStream: false, cancellationToken);
            await session.RequestStream.WriteDataAsync(chosen.Body, endStream: true, cancellationToken);
            return true;
        }

        var limit = WebTransportFlowControlSettings.IsOnFor(settings) ? SessionLimitWithFlowControl : SessionLimitWithoutFlowControl;
        if (!directory.TryOpenSession(limit))
        {
            await session.RequestStream.ResetAsync(Http3ErrorCode.RequestRejected, $"the connection holds {limit} WebTransport sessions already", cancellationToken);
            return true;
        }

        return false;
    }

    // ADR-0117 decision 1's order: the scheme (WTH3 3.2: https only), the origin, then the login (decision 7), then the path, so an unauthenticated client learns nothing of the paths served.
    private async Task<QpackFieldLine[]?> JudgeAsync(Http3RequestHead head, CancellationToken cancellationToken) =>
        !WebTransportConnectJudgement.IsSecureScheme(head) ? AnswerFields(WebTransportConnectJudgement.BadRequest)
        : !WebTransportConnectJudgement.IsOwnOrigin(head) ? AnswerFields(WebTransportConnectJudgement.Forbidden)
        : await authentication.RefuseAsync(head, cancellationToken) is { } refusal ? refusal
        : !WebTransportConnectJudgement.IsAcceptedPath(head.Target, acceptedPath) ? AnswerFields(WebTransportConnectJudgement.MethodNotAllowed)
        : null;

    // WTH3 3.1: no WebTransport request is processed before the client's SETTINGS; null when --head-timeout passes first.
    private async Task<IReadOnlyDictionary<ulong, ulong>?> ReadClientSettingsAsync(Http3ExtendedConnectSession session, CancellationToken cancellationToken)
    {
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var reading = session.ReadClientSettingsAsync(timer.Token);
        var timeout = Task.Delay(WebTransportHeadTimeoutDelay.For(context.Limits.HeadTimeout), context.TimeProvider, timer.Token);
        var first = await Task.WhenAny(reading, timeout);
        await timer.CancelAsync();
        return first == reading ? await reading : null;
    }

    private WebTransportFlowControl? FlowControlFor(IReadOnlyDictionary<ulong, ulong> settings) =>
        WebTransportFlowControlSettings.IsOnFor(settings)
            ? new WebTransportFlowControl(flowControlSettings, (ulong)context.Limits.MaxUploadBytes, settings)
            : null;

    private static bool MeetsRequirements(IReadOnlyDictionary<ulong, ulong> settings) =>
        settings.GetValueOrDefault(WebTransportHttp3Codes.H3DatagramSetting) == 1
        && settings.GetValueOrDefault(WebTransportHttp3Codes.EnabledSetting) == 1;
}
