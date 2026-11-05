namespace Surl.Quic;

/// <summary>
/// The streams of one connection and their flow control (RFC 9000 sections 2 to 4): it opens the
/// client's streams as their frames arrive, up to the <c>MAX_STREAMS</c> it granted, holds the
/// connection's credit both ways, acts on the stream and flow-control frames the client sends, and
/// cuts what the server's streams may send into frames. Credit is granted again as data is read
/// (<c>MAX_DATA</c>, <c>MAX_STREAM_DATA</c>) and as the client's streams close (<c>MAX_STREAMS</c>),
/// a window at a time, so the client never stalls on the server's credit (ADR-0082 decision 4).
/// </summary>
internal sealed class QuicStreams
{
    /// <summary>
    /// The most stream data one <c>STREAM</c> frame carries: the longest 1-RTT frame less the
    /// frame's longest type, stream ID, offset and two-byte length.
    /// </summary>
    public const int MaxStreamFrameData = QuicServerConnection.MaxApplicationFrameLength - 1 - 8 - 8 - 2;

    private const int Bidirectional = 0;

    private readonly SortedDictionary<ulong, QuicStream> open = [];
    private readonly List<QuicFrame> controlFrames = [];
    private readonly List<QuicStream> updated = [];
    private readonly HashSet<ulong> resetStreams = [];
    private readonly ulong[] clientStreamReceiveWindows;
    private readonly ulong[] clientStreamSendLimits;
    private readonly ulong[] initialClientStreamLimits;
    private readonly ulong[] clientStreamLimits;
    private readonly ulong[] clientStreamsOpened = new ulong[2];
    private readonly ulong[] clientStreamsClosed = new ulong[2];
    private readonly ulong receiveWindow;
    private readonly ulong serverStreamSendLimit;
    private ulong receiveLimit;
    private ulong received;
    private ulong consumed;
    private ulong sendLimit;
    private ulong sent;
    private ulong? dataBlockedReportedAt;
    private ulong serverStreamsOpened;
    private ulong serverStreamLimit;
    private ulong? streamsBlockedReportedAt;

    /// <summary>Starts with the credit the server's transport parameters grant and the client's grant.</summary>
    public QuicStreams(QuicServerConnectionOptions options, QuicTransportParameters peer)
    {
        clientStreamReceiveWindows = [options.InitialMaxStreamDataBidiRemote, options.InitialMaxStreamDataUni];
        clientStreamSendLimits = [peer.InitialMaxStreamDataBidiLocal ?? 0, 0];
        initialClientStreamLimits = [options.InitialMaxStreamsBidi, options.InitialMaxStreamsUni];
        clientStreamLimits = [.. initialClientStreamLimits];
        receiveWindow = options.InitialMaxData;
        receiveLimit = receiveWindow;
        sendLimit = peer.InitialMaxData ?? 0;
        serverStreamSendLimit = peer.InitialMaxStreamDataUni ?? 0;
        serverStreamLimit = peer.InitialMaxStreamsUni ?? 0;
    }

    /// <summary>How many more stream bytes the client's <c>MAX_DATA</c> lets the server send.</summary>
    public ulong ConnectionSendCredit => sendLimit - sent;

    /// <summary>Counts stream bytes put in frames against the client's <c>MAX_DATA</c>.</summary>
    public void RecordSent(ulong count) => sent += count;

    /// <summary>Counts stream bytes the application has read, or a reset discarded, and grants more connection credit when half the window is used.</summary>
    public void Consume(ulong count)
    {
        consumed += count;
        if (receiveLimit - consumed < receiveWindow / 2)
        {
            receiveLimit = consumed + receiveWindow;
            controlFrames.Add(new QuicMaxDataFrame(receiveLimit));
        }
    }

    /// <summary>Queues a frame the next datagrams carry: credit, <c>RESET_STREAM</c> or <c>STOP_SENDING</c>.</summary>
    public void QueueControlFrame(QuicFrame frame) => controlFrames.Add(frame);

    /// <summary>Records that a stream's sending part was reset, so its lost data is not sent again (RFC 9000 section 13.3).</summary>
    public void RecordReset(ulong streamId) => resetStreams.Add(streamId);

    /// <summary>
    /// What to send in place of a lost stream or flow-control frame (RFC 9000 section 13.3): the
    /// data of a stream not reset, the current <c>MAX_DATA</c> and <c>MAX_STREAMS</c>, and the
    /// current <c>MAX_STREAM_DATA</c> while the stream still receives; null for what is no longer
    /// needed. Every other frame is sent again as it was.
    /// </summary>
    public QuicFrame? Refresh(QuicFrame frame) => frame switch
    {
        QuicStreamFrame stream => resetStreams.Contains(stream.StreamId) ? null : stream,
        QuicMaxDataFrame => new QuicMaxDataFrame(receiveLimit),
        QuicMaxStreamsFrame maxStreams => new QuicMaxStreamsFrame(maxStreams.Bidirectional, clientStreamLimits[maxStreams.Bidirectional ? Bidirectional : 1]),
        QuicMaxStreamDataFrame maxStreamData => open.GetValueOrDefault(maxStreamData.StreamId) is { IsReceiveDone: false } stream
            ? new QuicMaxStreamDataFrame(stream.Id, stream.ReceiveLimit)
            : null,
        _ => frame,
    };

    /// <summary>Opens the server's next unidirectional stream, or reports <c>STREAMS_BLOCKED</c> and returns null when the client's <c>MAX_STREAMS</c> allows no more.</summary>
    public QuicStream? OpenUnidirectional()
    {
        if (serverStreamsOpened >= serverStreamLimit)
        {
            ReportStreamsBlocked();
            return null;
        }

        var stream = new QuicStream(this, (serverStreamsOpened++ << 2) | 3, 0, serverStreamSendLimit);
        open.Add(stream.Id, stream);
        return stream;
    }

    /// <summary>Acts on one of the stream and flow-control frames a client sends.</summary>
    /// <exception cref="QuicTransportException">The frame breaks a stream's state, a stream limit, a credit or a final size.</exception>
    public void Receive(QuicFrame frame)
    {
        switch (frame)
        {
            case QuicStreamFrame stream:
                ReceiveForStream(stream.StreamId, true, stream.FrameType(), target => ReceiveData(target.ReceiveData(stream), stream.FrameType()));
                break;
            case QuicResetStreamFrame reset:
                ReceiveForStream(reset.StreamId, true, QuicFrameTypes.ResetStream, target => ReceiveData(target.ReceiveReset(reset), QuicFrameTypes.ResetStream));
                break;
            case QuicStopSendingFrame stop:
                ReceiveForStream(stop.StreamId, false, QuicFrameTypes.StopSending, target => target.ReceiveStopSending(stop.ApplicationErrorCode));
                break;
            case QuicMaxStreamDataFrame maxStreamData:
                ReceiveForStream(maxStreamData.StreamId, false, QuicFrameTypes.MaxStreamData, target => target.ReceiveMaxStreamData(maxStreamData.MaximumStreamData));
                break;
            case QuicStreamDataBlockedFrame blocked:
                ReceiveForStream(blocked.StreamId, true, QuicFrameTypes.StreamDataBlocked, _ => { });
                break;
            case QuicMaxDataFrame maxData:
                sendLimit = Math.Max(sendLimit, maxData.MaximumData);
                break;
            default:
                ReceiveMaxStreams((QuicMaxStreamsFrame)frame);
                break;
        }
    }

    /// <summary>
    /// Puts what is waiting into <paramref name="frames"/>: stream data the credit allows,
    /// <c>DATA_BLOCKED</c> once per limit that holds data back, and the queued credit, reset and
    /// stop frames, with <c>MAX_STREAMS</c> for the client's streams that have closed.
    /// </summary>
    public void QueueFrames(List<QuicFrame> frames)
    {
        foreach (var stream in open.Values)
        {
            stream.QueueStreamFrames(frames);
        }

        ReportDataBlocked(frames);
        CloseFinishedStreams();
        frames.AddRange(controlFrames);
        controlFrames.Clear();
    }

    /// <summary>The streams frames have reached since the last call, in the order first reached.</summary>
    public List<QuicStream> TakeUpdated()
    {
        var streams = updated.ToList();
        updated.Clear();
        return streams;
    }

    private static int KindOf(ulong streamId) => (int)((streamId >> 1) & 1);

    private static QuicTransportException StreamStateError(ulong streamId, byte frameType) =>
        new(QuicTransportErrorCode.StreamStateError, $"Stream {streamId} is not open in the direction this frame needs (RFC 9000 section 3).", frameType);

    private void ReceiveForStream(ulong streamId, bool aboutReceiving, byte frameType, Action<QuicStream> act)
    {
        if (StreamFor(streamId, aboutReceiving, frameType) is { } stream)
        {
            act(stream);
            if (!updated.Contains(stream))
            {
                updated.Add(stream);
            }
        }
    }

    /// <summary>
    /// The stream a frame names, opening the client's streams up to it; null for a stream that has
    /// closed. A frame about the receiving part of a stream the server opened, about the sending
    /// part of a client's unidirectional stream, or naming a server stream not yet opened is
    /// <c>STREAM_STATE_ERROR</c> (RFC 9000 sections 19.4 to 19.13).
    /// </summary>
    private QuicStream? StreamFor(ulong streamId, bool aboutReceiving, byte frameType)
    {
        if ((streamId & 1) == 1)
        {
            return ServerStreamFor(streamId, aboutReceiving, frameType);
        }

        return KindOf(streamId) != Bidirectional && !aboutReceiving
            ? throw StreamStateError(streamId, frameType)
            : OpenClientStreamsUpTo(streamId, frameType);
    }

    private QuicStream? ServerStreamFor(ulong streamId, bool aboutReceiving, byte frameType) =>
        aboutReceiving || KindOf(streamId) == Bidirectional || (streamId >> 2) >= serverStreamsOpened
            ? throw StreamStateError(streamId, frameType)
            : open.GetValueOrDefault(streamId);

    /// <summary>Opens every client stream of the kind up to <paramref name="streamId"/> (RFC 9000 section 3.2); one beyond <c>MAX_STREAMS</c> is <c>STREAM_LIMIT_ERROR</c>.</summary>
    private QuicStream? OpenClientStreamsUpTo(ulong streamId, byte frameType)
    {
        var kind = KindOf(streamId);
        if ((streamId >> 2) >= clientStreamLimits[kind])
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.StreamLimitError, $"Stream {streamId} is beyond the {clientStreamLimits[kind]} streams of its kind granted (RFC 9000 section 4.6).", frameType);
        }

        for (; clientStreamsOpened[kind] <= streamId >> 2; clientStreamsOpened[kind]++)
        {
            var id = (clientStreamsOpened[kind] << 2) | ((ulong)kind << 1);
            open.Add(id, new QuicStream(this, id, clientStreamReceiveWindows[kind], clientStreamSendLimits[kind]));
        }

        return open.GetValueOrDefault(streamId);
    }

    /// <summary>Counts how far a frame raised a stream's highest offset against the connection's credit (RFC 9000 section 4.1).</summary>
    private void ReceiveData(ulong growth, byte frameType)
    {
        received += growth;
        if (received > receiveLimit)
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.FlowControlError, $"The streams hold {received} bytes, past the {receiveLimit} bytes of connection credit granted (RFC 9000 section 4.1).", frameType);
        }
    }

    /// <summary>The client's credit for the server's unidirectional streams only grows; the server opens no bidirectional stream.</summary>
    private void ReceiveMaxStreams(QuicMaxStreamsFrame frame)
    {
        if (!frame.Bidirectional)
        {
            serverStreamLimit = Math.Max(serverStreamLimit, frame.MaximumStreams);
        }
    }

    private void ReportStreamsBlocked()
    {
        if (streamsBlockedReportedAt != serverStreamLimit)
        {
            streamsBlockedReportedAt = serverStreamLimit;
            controlFrames.Add(new QuicStreamsBlockedFrame(false, serverStreamLimit));
        }
    }

    private void ReportDataBlocked(List<QuicFrame> frames)
    {
        if (sent == sendLimit && dataBlockedReportedAt != sendLimit && open.Values.Any(stream => stream.UnsentLength > 0))
        {
            dataBlockedReportedAt = sendLimit;
            frames.Add(new QuicDataBlockedFrame(sendLimit));
        }
    }

    /// <summary>Forgets every stream both of whose parts are done, and grants the client a new stream for each of its own that closed (RFC 9000 section 4.6).</summary>
    private void CloseFinishedStreams()
    {
        foreach (var stream in open.Values.Where(stream => stream.IsReceiveDone && stream.IsSendDone).ToList())
        {
            open.Remove(stream.Id);
            if (stream.IsClientInitiated)
            {
                clientStreamsClosed[KindOf(stream.Id)]++;
            }
        }

        for (var kind = 0; kind < 2; kind++)
        {
            var limit = clientStreamsClosed[kind] + initialClientStreamLimits[kind];
            if (limit > clientStreamLimits[kind])
            {
                clientStreamLimits[kind] = limit;
                controlFrames.Add(new QuicMaxStreamsFrame(kind == Bidirectional, limit));
            }
        }
    }
}
