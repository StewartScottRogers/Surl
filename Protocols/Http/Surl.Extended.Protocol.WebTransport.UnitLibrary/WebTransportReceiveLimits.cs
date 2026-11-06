namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// The flow-control limits one session's client is held to, with flow control on (ADR-0117 decision 4;
/// WTH3 5.6): the client streams of each direction against <c>WT_MAX_STREAMS</c> and the stream payload
/// the server has read against <c>WT_MAX_DATA</c>, each limit raised as the session uses it.
/// </summary>
/// <remarks>
/// A stream limit is raised to the streams closed plus the initial credit once half of what is left
/// is used; the data limit to the bytes read plus the window once half the window is read, never
/// past <paramref name="maxData"/> (<c>--max-filesize</c>, decision 8). Every value raised is above
/// the last. Safe to call from every stream of the session at once.
/// </remarks>
/// <param name="settings">The initial credit and the window.</param>
/// <param name="maxData">The most the data limit is ever raised to.</param>
internal sealed class WebTransportReceiveLimits(WebTransportFlowControlSettings settings, ulong maxData)
{
    private readonly Lock gate = new();
    private readonly StreamCount bidirectional = new(settings.InitialMaxStreams);
    private readonly StreamCount unidirectional = new(settings.InitialMaxStreams);
    private ulong dataLimit = settings.DataWindow;
    private ulong consumed;

    /// <summary>Counts a new client stream: whether it is within <c>WT_MAX_STREAMS</c>.</summary>
    /// <param name="isBidirectional">Whether the stream is bidirectional.</param>
    /// <param name="raise">A raised <c>WT_MAX_STREAMS</c> to send, or <see langword="null"/>.</param>
    /// <returns><see langword="false"/> when the client opened more streams than allowed.</returns>
    public bool TryOpenStream(bool isBidirectional, out WebTransportLimitCapsule? raise)
    {
        lock (gate)
        {
            var count = Count(isBidirectional);
            var isWithin = ++count.Opened <= count.Limit;
            raise = isWithin ? Raise(isBidirectional) : null;
            return isWithin;
        }
    }

    /// <summary>Counts a client stream that has ended.</summary>
    /// <param name="isBidirectional">Whether the stream is bidirectional.</param>
    /// <returns>A raised <c>WT_MAX_STREAMS</c> to send, or <see langword="null"/>.</returns>
    public WebTransportLimitCapsule? CloseStream(bool isBidirectional)
    {
        lock (gate)
        {
            Count(isBidirectional).Closed++;
            return Raise(isBidirectional);
        }
    }

    /// <summary>Counts <paramref name="bytes"/> of stream payload read: whether they are within <c>WT_MAX_DATA</c>.</summary>
    /// <param name="bytes">The payload bytes read.</param>
    /// <param name="raise">A raised <c>WT_MAX_DATA</c> to send, or <see langword="null"/>.</param>
    /// <returns><see langword="false"/> when the client sent more than allowed.</returns>
    public bool TryConsume(ulong bytes, out WebTransportLimitCapsule? raise)
    {
        lock (gate)
        {
            consumed += bytes;
            raise = null;
            if (consumed > dataLimit)
            {
                return false;
            }

            var raised = Math.Min(consumed + settings.DataWindow, maxData);
            if (consumed >= dataLimit - (settings.DataWindow / 2) && raised > dataLimit)
            {
                dataLimit = raised;
                raise = new WebTransportLimitCapsule(WebTransportCapsuleType.MaxData, raised);
            }

            return true;
        }
    }

    private StreamCount Count(bool isBidirectional) => isBidirectional ? bidirectional : unidirectional;

    private WebTransportLimitCapsule? Raise(bool isBidirectional)
    {
        var count = Count(isBidirectional);
        var raised = count.Closed + settings.InitialMaxStreams;
        if (raised <= count.Limit || count.Limit - count.Opened > settings.InitialMaxStreams / 2)
        {
            return null;
        }

        count.Limit = raised;
        var type = isBidirectional ? WebTransportCapsuleType.MaxStreamsBidirectional : WebTransportCapsuleType.MaxStreamsUnidirectional;
        return new WebTransportLimitCapsule(type, raised);
    }

    private sealed class StreamCount(ulong limit)
    {
        public ulong Limit { get; set; } = limit;

        public ulong Opened { get; set; }

        public ulong Closed { get; set; }
    }
}
