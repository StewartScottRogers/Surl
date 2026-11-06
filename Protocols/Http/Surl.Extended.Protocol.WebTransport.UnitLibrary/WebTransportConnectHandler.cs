using Surl.Http3;
using Surl.Protocol.Abstractions;
using Surl.Qpack;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The extended <c>CONNECT</c> handler for <c>webtransport-h3</c> on one connection (ADR-0117
/// decisions 0 and 1): it waits for the client's <c>SETTINGS</c>, judges the request, answers
/// <c>200</c> and serves the session with a <see cref="WebTransportSession"/>.
/// </summary>
/// <param name="acceptedPath">The listen URL's path; empty or <c>/</c> accepts every path.</param>
/// <param name="directory">The connection's sessions, for the streams that name them.</param>
/// <param name="context">The exchange: its clock, limits and log.</param>
internal sealed class WebTransportConnectHandler(string acceptedPath, WebTransportSessionDirectory directory, ExchangeContext context) : IHttp3ExtendedConnectHandler
{
    /// <summary>The <c>:protocol</c> WTH3 3.2 names.</summary>
    public const string ProtocolName = "webtransport-h3";

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
        if (await RefuseAsync(session, cancellationToken))
        {
            directory.Settle(session.Id, null);
            return;
        }

        await session.RequestStream.WriteHeadersAsync(AnswerFields(WebTransportConnectJudgement.Accepted), endStream: false, cancellationToken);
        context.Log.Note($"WebTransport session {session.Id} on {session.Head.Target} was accepted.");
        using var served = new WebTransportSession(session, context);
        directory.Settle(session.Id, served);
        try
        {
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

    // Answers or resets a CONNECT the server does not accept, and says whether it did.
    private async Task<bool> RefuseAsync(Http3ExtendedConnectSession session, CancellationToken cancellationToken)
    {
        if (await ReadClientSettingsAsync(session, cancellationToken) is not { } settings)
        {
            await session.RequestStream.ResetAsync(Http3ErrorCode.RequestIncomplete, "the client's SETTINGS did not arrive within the head timeout", cancellationToken);
            return true;
        }

        if (!MeetsRequirements(settings))
        {
            await session.RequestStream.ResetAsync((Http3ErrorCode)WebTransportHttp3Codes.RequirementsNotMet, "the client's SETTINGS lack SETTINGS_H3_DATAGRAM = 1 or SETTINGS_WT_ENABLED = 1", cancellationToken);
            return true;
        }

        var status = WebTransportConnectJudgement.Judge(session.Head, acceptedPath);
        if (status == WebTransportConnectJudgement.Accepted)
        {
            return false;
        }

        context.Log.Note($"WebTransport CONNECT {session.Id} to {session.Head.Target} was answered {status}.");
        await session.RequestStream.WriteHeadersAsync(AnswerFields(status), endStream: true, cancellationToken);
        return true;
    }

    // WTH3 3.1: no WebTransport request is processed before the client's SETTINGS; null when --head-timeout passes first.
    private async Task<IReadOnlyDictionary<ulong, ulong>?> ReadClientSettingsAsync(Http3ExtendedConnectSession session, CancellationToken cancellationToken)
    {
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var reading = session.ReadClientSettingsAsync(timer.Token);
        var timeout = Task.Delay(context.Limits.HeadTimeout, context.TimeProvider, timer.Token);
        var first = await Task.WhenAny(reading, timeout);
        await timer.CancelAsync();
        return first == reading ? await reading : null;
    }

    private static bool MeetsRequirements(IReadOnlyDictionary<ulong, ulong> settings) =>
        settings.GetValueOrDefault(WebTransportHttp3Codes.H3DatagramSetting) == 1
        && settings.GetValueOrDefault(WebTransportHttp3Codes.EnabledSetting) == 1;
}
