namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// One session's flow control, both ways, when it is on for the connection (ADR-0117 decision 4;
/// WTH3 5): the limits the client is held to, and the client's limits on what the server sends,
/// starting from the client's <c>SETTINGS_WT_INITIAL_*</c> and raised by its <c>WT_MAX_DATA</c> and
/// <c>WT_MAX_STREAMS</c>.
/// </summary>
internal sealed class WebTransportFlowControl
{
    /// <summary>The largest <c>WT_MAX_STREAMS</c> a client may send (WTH3 5.6.3: 2^60).</summary>
    public const ulong MaxStreamCount = 1UL << 60;

    /// <summary>Creates the session's flow control.</summary>
    /// <param name="settings">The limits the server offers.</param>
    /// <param name="maxData">The most the server's data limit is raised to (<c>--max-filesize</c>).</param>
    /// <param name="clientSettings">The client's <c>SETTINGS</c>.</param>
    public WebTransportFlowControl(WebTransportFlowControlSettings settings, ulong maxData, IReadOnlyDictionary<ulong, ulong> clientSettings)
    {
        Receive = new WebTransportReceiveLimits(settings, maxData);
        SendData = new WebTransportSendCredit(clientSettings.GetValueOrDefault(WebTransportFlowControlSettings.InitialMaxDataSetting));
        SendUnidirectionalStreams = new WebTransportSendCredit(clientSettings.GetValueOrDefault(WebTransportFlowControlSettings.InitialMaxStreamsUnidirectionalSetting));
        SendBidirectionalStreams = new WebTransportSendCredit(clientSettings.GetValueOrDefault(WebTransportFlowControlSettings.InitialMaxStreamsBidirectionalSetting));
    }

    /// <summary>The limits the client is held to.</summary>
    public WebTransportReceiveLimits Receive { get; }

    /// <summary>The client's <c>WT_MAX_DATA</c>: the echoed bytes the server may send.</summary>
    public WebTransportSendCredit SendData { get; }

    /// <summary>The client's unidirectional <c>WT_MAX_STREAMS</c>: the answering streams the server may open.</summary>
    public WebTransportSendCredit SendUnidirectionalStreams { get; }

    /// <summary>The client's bidirectional <c>WT_MAX_STREAMS</c>; the server opens no bidirectional stream, so it is only checked.</summary>
    public WebTransportSendCredit SendBidirectionalStreams { get; }

    /// <summary>
    /// Takes a client <c>WT_MAX_DATA</c> or <c>WT_MAX_STREAMS</c> (WTH3 5.6.2, 5.6.3); the
    /// <c>*_BLOCKED</c> capsules change nothing.
    /// </summary>
    /// <param name="capsule">The client's limit capsule.</param>
    /// <returns><see langword="false"/> for a limit that does not increase the last, or a stream limit above 2^60: a flow-control error.</returns>
    public bool TryApplyClientLimit(WebTransportLimitCapsule capsule)
    {
        if (capsule.Type == WebTransportCapsuleType.MaxData)
        {
            return SendData.TryRaise(capsule.Limit);
        }

        var streams = capsule.Type == WebTransportCapsuleType.MaxStreamsUnidirectional ? SendUnidirectionalStreams
            : capsule.Type == WebTransportCapsuleType.MaxStreamsBidirectional ? SendBidirectionalStreams
            : null;
        return streams is null || (capsule.Limit <= MaxStreamCount && streams.TryRaise(capsule.Limit));
    }
}
