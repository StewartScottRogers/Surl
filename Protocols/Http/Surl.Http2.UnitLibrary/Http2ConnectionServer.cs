using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Surl.Hpack;
using Surl.Protocol.Abstractions;

namespace Surl.Http2;

/// <summary>
/// Serves one HTTP/2 connection whose client preface has been read (RFC 9113; ADR-0081,
/// decisions 5 to 7; ADR-0086, decision 1): reads the client's frames in order, hands each
/// request stream to the <see cref="IHttp2StreamHandler"/> on a task of its own, and ends the connection with
/// <c>GOAWAY</c> when the client breaks a rule or a limit, or the exchange is cancelled.
/// </summary>
/// <remarks>
/// <para>
/// The server's first frame is <c>SETTINGS</c> with <c>MAX_CONCURRENT_STREAMS</c> 100 and
/// <c>MAX_HEADER_LIST_SIZE</c> <c>--max-message</c> (left out when it is 0, no limit). It does not
/// wait for its <c>SETTINGS</c> to be acknowledged; it acknowledges each of the client's, applies
/// <c>HEADER_TABLE_SIZE</c> to its encoder and <c>INITIAL_WINDOW_SIZE</c> to its send windows, and
/// ignores the rest. A <c>PING</c> is answered with an ACK of the same 8 bytes, and <c>PRIORITY</c>
/// and the client's <c>GOAWAY</c> change nothing. The first frame after the preface must be
/// <c>SETTINGS</c>.
/// </para>
/// <para>
/// A request stream opens with a header block, <c>HEADERS</c> and its <c>CONTINUATION</c>s,
/// decoded with one HPACK decoder for the whole connection. A block longer than
/// <c>--max-message</c>, or that decodes to a list past it, ends the connection with
/// <c>ENHANCE_YOUR_CALM</c>, as does one not complete within <c>--head-timeout</c>; one HPACK
/// cannot decode ends it with <c>COMPRESSION_ERROR</c>. A request opening a 101st open stream is
/// reset <c>REFUSED_STREAM</c>, a malformed one (<see cref="Http2RequestHeadMapping"/>), or one
/// whose priority names its own stream, <c>PROTOCOL_ERROR</c>; the connection carries on. A
/// header block on a stream already used is trailers: it ends an open stream's body when it
/// ends the stream and resets the stream <c>PROTOCOL_ERROR</c> otherwise, and is ignored once
/// the stream is closed.
/// </para>
/// <para>
/// <c>DATA</c> is the stream's request body. The connection's receive window is replenished as
/// each <c>DATA</c> arrives, so only a stream's window can be overrun, which ends the connection
/// with <c>FLOW_CONTROL_ERROR</c>; a stream's window is replenished as its handler reads the
/// body. <c>DATA</c> after the client ended its half is reset <c>STREAM_CLOSED</c>, and
/// <c>DATA</c> on a stream that is closed is ignored. A handler that returns leaving the client's
/// half open - a refusal, or a body left unread - is followed by <c>RST_STREAM</c> <c>NO_ERROR</c>.
/// A <c>PING</c> is answered here; no handler sees it.
/// </para>
/// <para>
/// When the client closes, the open streams finish what their windows let them send. When the
/// exchange is cancelled - a limit or shutdown (ADR-0059) - the streams are cut off; a handler
/// that catches it may end its stream once more with <see cref="Http2RequestStream.WriteFarewellTrailersAsync"/>
/// or <see cref="Http2RequestStream.WriteFarewellResetAsync"/>, which every handler finishes before
/// the connection's own farewell (ADR-0116, amendment 1). Then
/// <c>GOAWAY</c> naming the highest stream received, <c>NO_ERROR</c>, is written within
/// <see cref="FarewellWriteDeadline"/>; the cancellation then escapes at shutdown and not for a
/// limit.
/// </para>
/// </remarks>
public sealed class Http2ConnectionServer
{
    /// <summary>The most streams a client may have open at once (ADR-0081, decision 5).</summary>
    public const int MaxConcurrentStreams = 100;

    /// <summary>
    /// How long the farewell <c>GOAWAY</c> of a cancelled exchange may take to write
    /// (ADR-0006, section 5; ADR-0059, decision 3).
    /// </summary>
    public static readonly TimeSpan FarewellWriteDeadline = TimeSpan.FromSeconds(1);

    private readonly IHttp2StreamHandler handler;
    private readonly ExchangeContext context;
    private readonly IConnection connection;
    private readonly Http2FrameReader reader;
    private readonly Http2FrameWriter frames;
    private readonly Http2SendWindows windows = new();
    private readonly HpackDecoder decoder;
    private readonly ConcurrentDictionary<int, Http2Stream> streams = new();
    private readonly List<Task> answering = [];
    private readonly MemoryStream headerBlock = new();
    private Http2HeadersFrame? headerBlockStart;
    private CancellationTokenSource? headerBlockTimeout;
    private bool headerBlockOpensStream;
    private int lastStreamId;
    private bool hasReadClientSettings;

    /// <summary>
    /// Creates a server for one connection whose client preface has been read.
    /// </summary>
    /// <param name="handler">Serves every request stream that passed validation.</param>
    /// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
    /// <param name="connection">The connection, positioned just after the client preface.</param>
    public Http2ConnectionServer(IHttp2StreamHandler handler, ExchangeContext context, IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(connection);
        this.handler = handler;
        this.context = context;
        this.connection = connection;
        reader = new Http2FrameReader(connection, Http2FrameReader.DefaultMaxFrameSize);
        frames = new Http2FrameWriter(connection);
        decoder = new HpackDecoder(HpackDecoder.DefaultMaximumTableSize, (int)Math.Min(NoLimitAsMax(context.Limits.MaxMessageBytes), int.MaxValue));
    }

    private CancellationToken ConnectionToken => context.CancellationToken;

    /// <summary>
    /// Serves the connection until the client closes it, a connection error ends it, or the
    /// exchange is cancelled.
    /// </summary>
    /// <returns>A task that completes when the exchange is over.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled at shutdown.</exception>
    public Task ServeAsync() => ServeAsync(ReadFramesAsync);

    /// <summary>
    /// Serves a connection upgraded from HTTP/1.1 with <c>Upgrade: h2c</c> once its <c>101</c> is
    /// written (RFC 7540 section 3.2; ADR-0091): the server's <c>SETTINGS</c> goes out at once,
    /// <paramref name="clientSettings"/> are applied as the client's first <c>SETTINGS</c>, which
    /// the <c>101</c> acknowledged, and once the client's preface has been read
    /// <paramref name="upgradedRequest"/> is answered on stream 1, half-closed by the client.
    /// Bytes other than the preface end the connection with <c>GOAWAY</c> <c>PROTOCOL_ERROR</c>,
    /// last stream 0. After that it is served as <see cref="ServeAsync()"/> serves.
    /// </summary>
    /// <param name="upgradedRequest">The head of the HTTP/1.1 request that asked for the upgrade, which has no body.</param>
    /// <param name="clientSettings">The settings its <c>HTTP2-Settings</c> field carried (<see cref="Http2SettingsPayload"/>).</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    /// <exception cref="OperationCanceledException">The exchange was cancelled at shutdown.</exception>
    public Task ServeUpgradedAsync(Http2RequestHead upgradedRequest, IReadOnlyList<Http2Setting> clientSettings) =>
        ServeAsync(() => ServeUpgradedStreamThenReadFramesAsync(upgradedRequest, clientSettings));

    private async Task ServeAsync(Func<Task<Http2ConnectionError?>> readFramesAsync)
    {
        ExceptionDispatchInfo? cancellation = null;
        try
        {
            await frames.WriteAsync(ServerSettings(context.Limits), ConnectionToken);
            await EndAsync(await readFramesAsync());
        }
        catch (OperationCanceledException exception)
        {
            cancellation = ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            headerBlockTimeout?.Dispose();
        }

        if (cancellation is null)
        {
            return;
        }

        await Task.WhenAll(answering);
        await WriteFarewellAsync();
        if (context.IsCancelledForALimit)
        {
            return;
        }

        cancellation.Throw();
    }

    private static long NoLimitAsMax(long limit) => limit == 0 ? long.MaxValue : limit;

    private static Http2SettingsFrame ServerSettings(ExchangeLimits limits)
    {
        List<Http2Setting> settings = [new(Http2SettingIdentifier.MaxConcurrentStreams, MaxConcurrentStreams)];
        if (limits.MaxMessageBytes != 0)
        {
            settings.Add(new(Http2SettingIdentifier.MaxHeaderListSize, (uint)Math.Min(limits.MaxMessageBytes, uint.MaxValue)));
        }

        return new Http2SettingsFrame(false, settings);
    }

    // The upgraded request is stream 1 (RFC 7540 section 3.2), opened only once the client's
    // preface is read, so the client sees its answer after it has spoken HTTP/2 itself, as
    // measured. The preface is read exactly, leaving its first frames to the frame reader.
    private async Task<Http2ConnectionError?> ServeUpgradedStreamThenReadFramesAsync(Http2RequestHead upgradedRequest, IReadOnlyList<Http2Setting> clientSettings)
    {
        // No stream is open yet, so INITIAL_WINDOW_SIZE cannot overflow one: this never fails.
        _ = await ApplySettingsAsync(clientSettings);
        var preface = new Http2ConnectionPreface(Http2ConnectionPreface.Preface.Length);
        await preface.ReadAsync(connection, ConnectionToken);
        if (!preface.IsComplete)
        {
            return new(Http2ErrorCode.ProtocolError, $"after the 101 the client sent {preface.BytesRead.Length} bytes that are not the HTTP/2 connection preface");
        }

        lastStreamId = 1;
        OpenStream(new Http2HeadersFrame(1, [], EndStream: true, EndHeaders: true, null), upgradedRequest);

        return await ReadFramesAsync();
    }

    // Null when the client closed; otherwise the connection error that ends the connection.
    private async Task<Http2ConnectionError?> ReadFramesAsync()
    {
        while (true)
        {
            var result = await ReadNextFrameAsync();
            if (result is null)
            {
                return new(Http2ErrorCode.EnhanceYourCalm, $"the header block of stream {headerBlockStart!.StreamId} was not complete within the head timeout");
            }

            if (result.Outcome is Http2FrameReadOutcome.ConnectionClosed or Http2FrameReadOutcome.ConnectionClosedMidFrame)
            {
                context.Log.Note($"The client closed the HTTP/2 connection: {result.Outcome}.");

                return null;
            }

            var error = await AcceptAsync(result);
            if (error is not null)
            {
                return error;
            }
        }
    }

    // Null when the head timeout ran out part way through a header block.
    private async Task<Http2FrameReadResult?> ReadNextFrameAsync()
    {
        if (headerBlockTimeout is null)
        {
            return await reader.ReadFrameAsync(ConnectionToken);
        }

        using var timeoutOrExchange = CancellationTokenSource.CreateLinkedTokenSource(ConnectionToken, headerBlockTimeout.Token);
        try
        {
            return await reader.ReadFrameAsync(timeoutOrExchange.Token);
        }
        catch (OperationCanceledException) when (!ConnectionToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private Task<Http2ConnectionError?> AcceptAsync(Http2FrameReadResult result)
    {
        if (OrderError(result.Frame) is { } error)
        {
            return Task.FromResult<Http2ConnectionError?>(error);
        }

        return result.Outcome switch
        {
            Http2FrameReadOutcome.FrameRead => AcceptFrameAsync(result.Frame!),
            Http2FrameReadOutcome.Malformed => AcceptMalformedAsync(result.Header!.Value, result.Malformation!.Value),
            _ => Task.FromResult<Http2ConnectionError?>(null),
        };
    }

    // The first frame must be the client's SETTINGS (RFC 9113 section 3.4), and nothing but the
    // CONTINUATIONs of a header block may come between its HEADERS and END_HEADERS (section 6.10).
    private Http2ConnectionError? OrderError(Http2Frame? frame)
    {
        if (!hasReadClientSettings)
        {
            hasReadClientSettings = true;
            if (frame is not Http2SettingsFrame { Ack: false })
            {
                return new(Http2ErrorCode.ProtocolError, "the client preface did not go on with a SETTINGS frame");
            }
        }

        return headerBlockStart is { } start && !ContinuesHeaderBlock(frame, start.StreamId)
            ? new(Http2ErrorCode.ProtocolError, $"a frame other than CONTINUATION interrupted the header block of stream {start.StreamId}")
            : null;
    }

    private static bool ContinuesHeaderBlock(Http2Frame? frame, int streamId) =>
        frame is Http2ContinuationFrame continuation && continuation.StreamId == streamId;

    private async Task<Http2ConnectionError?> AcceptMalformedAsync(Http2FrameHeader header, Http2FrameMalformation malformation)
    {
        var code = Http2FrameMalformations.ErrorCodeOf(malformation);
        if (Http2FrameMalformations.IsConnectionError(malformation))
        {
            return new(code, $"a frame of type {header.Type} on stream {header.StreamId} was malformed: {malformation}");
        }

        await ResetStreamAsync(header.StreamId, code, $"a frame of type {header.Type} was malformed: {malformation}");

        return null;
    }

    private Task<Http2ConnectionError?> AcceptFrameAsync(Http2Frame frame) => frame.StreamId == 0
        ? AcceptConnectionFrameAsync(frame)
        : AcceptStreamFrameAsync(frame);

    private Task<Http2ConnectionError?> AcceptConnectionFrameAsync(Http2Frame frame) => frame switch
    {
        Http2SettingsFrame settings => AcceptSettingsAsync(settings),
        Http2PingFrame ping => AcceptPingAsync(ping),
        Http2WindowUpdateFrame update => Task.FromResult(windows.GrowConnectionWindow(update.Increment)
            ? null
            : new Http2ConnectionError(Http2ErrorCode.FlowControlError, "a WINDOW_UPDATE grew the connection's send window past 2^31-1")),
        _ => Task.FromResult(AcceptGoAway((Http2GoAwayFrame)frame)),
    };

    private Task<Http2ConnectionError?> AcceptStreamFrameAsync(Http2Frame frame) => frame switch
    {
        Http2HeadersFrame headers => AcceptHeadersAsync(headers),
        Http2ContinuationFrame continuation => AcceptContinuationAsync(continuation),
        Http2DataFrame data => AcceptDataAsync(data),
        _ => AcceptStreamControlFrameAsync(frame),
    };

    private Task<Http2ConnectionError?> AcceptStreamControlFrameAsync(Http2Frame frame) => frame switch
    {
        Http2WindowUpdateFrame update => AcceptStreamWindowUpdateAsync(update),
        Http2RstStreamFrame reset => Task.FromResult(AcceptRstStream(reset)),
        Http2PushPromiseFrame => Task.FromResult<Http2ConnectionError?>(new(Http2ErrorCode.ProtocolError, "the client sent PUSH_PROMISE")),
        _ => Task.FromResult<Http2ConnectionError?>(null),
    };

    private async Task<Http2ConnectionError?> AcceptSettingsAsync(Http2SettingsFrame settings)
    {
        if (settings.Ack)
        {
            return null;
        }

        if (await ApplySettingsAsync(settings.Settings) is { } error)
        {
            return error;
        }

        await frames.WriteAsync(Http2SettingsFrame.Acknowledgement, ConnectionToken);

        return null;
    }

    private async Task<Http2ConnectionError?> ApplySettingsAsync(IReadOnlyList<Http2Setting> settings)
    {
        foreach (var setting in settings)
        {
            if (setting.Identifier == Http2SettingIdentifier.HeaderTableSize)
            {
                await frames.SetClientHeaderTableSizeAsync(setting.Value, ConnectionToken);
            }
            else if (setting.Identifier == Http2SettingIdentifier.InitialWindowSize && !windows.ChangeInitialStreamWindow(setting.Value))
            {
                return new(Http2ErrorCode.FlowControlError, "a SETTINGS_INITIAL_WINDOW_SIZE grew a stream's send window past 2^31-1");
            }
        }

        return null;
    }

    private async Task<Http2ConnectionError?> AcceptPingAsync(Http2PingFrame ping)
    {
        if (!ping.Ack)
        {
            await frames.WriteAsync(ping with { Ack = true }, ConnectionToken);
        }

        return null;
    }

    private Http2ConnectionError? AcceptGoAway(Http2GoAwayFrame goAway)
    {
        context.Log.Note($"The client sent GOAWAY {goAway.ErrorCode}, last stream {goAway.LastStreamId}.");

        return null;
    }

    private Http2ConnectionError? AcceptRstStream(Http2RstStreamFrame reset)
    {
        if (reset.StreamId > lastStreamId)
        {
            return new(Http2ErrorCode.ProtocolError, $"RST_STREAM on stream {reset.StreamId}, which was never opened");
        }

        if (streams.TryGetValue(reset.StreamId, out var stream))
        {
            context.Log.Note($"The client reset stream {reset.StreamId} with {reset.ErrorCode}.");
            stream.Reset();
        }

        return null;
    }

    private async Task<Http2ConnectionError?> AcceptStreamWindowUpdateAsync(Http2WindowUpdateFrame update)
    {
        if (update.StreamId > lastStreamId)
        {
            return new(Http2ErrorCode.ProtocolError, $"WINDOW_UPDATE on stream {update.StreamId}, which was never opened");
        }

        if (!windows.GrowStreamWindow(update.StreamId, update.Increment))
        {
            await ResetStreamAsync(update.StreamId, Http2ErrorCode.FlowControlError, "a WINDOW_UPDATE grew the stream's send window past 2^31-1");
        }

        return null;
    }

    private async Task<Http2ConnectionError?> AcceptHeadersAsync(Http2HeadersFrame headers)
    {
        if (headers.StreamId % 2 == 0)
        {
            return new(Http2ErrorCode.ProtocolError, $"HEADERS on stream {headers.StreamId}: a client opens odd-numbered streams");
        }

        headerBlockOpensStream = headers.StreamId > lastStreamId;
        lastStreamId = Math.Max(lastStreamId, headers.StreamId);
        headerBlockStart = headers;
        headerBlockTimeout = new CancellationTokenSource(Http2HeadTimeoutDelay.For(context.Limits.HeadTimeout), context.TimeProvider);

        return await AppendToHeaderBlockAsync(headers.FieldBlockFragment, headers.EndHeaders);
    }

    private async Task<Http2ConnectionError?> AcceptContinuationAsync(Http2ContinuationFrame continuation) =>
        headerBlockStart is null
            ? new(Http2ErrorCode.ProtocolError, $"CONTINUATION on stream {continuation.StreamId} with no header block to continue")
            : await AppendToHeaderBlockAsync(continuation.FieldBlockFragment, continuation.EndHeaders);

    private async Task<Http2ConnectionError?> AppendToHeaderBlockAsync(byte[] fragment, bool endHeaders)
    {
        headerBlock.Write(fragment);
        if (headerBlock.Length > NoLimitAsMax(context.Limits.MaxMessageBytes))
        {
            return new(Http2ErrorCode.EnhanceYourCalm, $"the header block of stream {headerBlockStart!.StreamId} is longer than the {context.Limits.MaxMessageBytes}-byte message limit");
        }

        return endHeaders ? await CompleteHeaderBlockAsync() : null;
    }

    private async Task<Http2ConnectionError?> CompleteHeaderBlockAsync()
    {
        var start = headerBlockStart!;
        var decoded = decoder.Decode(headerBlock.ToArray());
        headerBlock.SetLength(0);
        headerBlockStart = null;
        headerBlockTimeout!.Dispose();
        headerBlockTimeout = null;
        if (!decoded.IsDecoded)
        {
            return decoded.Error == HpackDecodingError.HeaderListTooLarge
                ? new(Http2ErrorCode.EnhanceYourCalm, $"the header list of stream {start.StreamId} is larger than the {context.Limits.MaxMessageBytes}-byte message limit")
                : new(Http2ErrorCode.CompressionError, $"the header block of stream {start.StreamId} could not be decoded: {decoded.Error}");
        }

        await (headerBlockOpensStream ? AcceptRequestAsync(start, decoded.Fields) : AcceptTrailersAsync(start));

        return null;
    }

    private async Task AcceptTrailersAsync(Http2HeadersFrame start)
    {
        if (!streams.TryGetValue(start.StreamId, out var stream))
        {
            context.Log.Note($"A header block on stream {start.StreamId}, which is closed, was ignored.");

            return;
        }

        if (!start.EndStream || !stream.Body.IsStillArriving)
        {
            await ResetStreamAsync(start.StreamId, Http2ErrorCode.ProtocolError, "trailers that do not end the stream, or after the stream's END_STREAM");

            return;
        }

        stream.Body.End();
    }

    private async Task AcceptRequestAsync(Http2HeadersFrame start, IReadOnlyList<HpackHeaderField> fields)
    {
        var (head, malformation) = Http2RequestHeadMapping.Map(fields);
        if (start.Priority?.StreamDependency == start.StreamId)
        {
            await ResetStreamAsync(start.StreamId, Http2ErrorCode.ProtocolError, "its priority names the stream itself");
        }
        else if (streams.Count >= MaxConcurrentStreams)
        {
            await ResetStreamAsync(start.StreamId, Http2ErrorCode.RefusedStream, $"it would be past the {MaxConcurrentStreams}-stream limit");
        }
        else if (head is null)
        {
            await ResetStreamAsync(start.StreamId, Http2ErrorCode.ProtocolError, $"the request is malformed: {malformation}");
        }
        else
        {
            OpenStream(start, head);
        }
    }

    private void OpenStream(Http2HeadersFrame start, Http2RequestHead head)
    {
        var streamId = start.StreamId;
        Http2Stream? stream = null;
        var body = new Http2RequestBody(!start.EndStream, head.ContentLength, increment => ReplenishStreamAsync(stream!, increment));
        stream = new Http2Stream(streamId, body, ConnectionToken);
        streams[streamId] = stream;
        windows.OpenStream(streamId);
        answering.RemoveAll(task => task.IsCompletedSuccessfully);
        var requestStream = new Http2RequestStream(stream, head, frames, windows, (code, reason) => ResetStreamAsync(streamId, code, reason), ConnectionToken, context.TimeProvider);
        answering.Add(ServeStreamAsync(stream, requestStream));
    }

    private async Task ServeStreamAsync(Http2Stream stream, Http2RequestStream requestStream)
    {
        try
        {
            await handler.ServeStreamAsync(requestStream, stream.Token);
            if (stream.Body.IsStillArriving)
            {
                await frames.WriteAsync(new Http2RstStreamFrame(stream.Id, Http2ErrorCode.NoError), ConnectionToken);
            }
        }
        catch (OperationCanceledException) when (stream.Token.IsCancellationRequested)
        {
            // The client reset the stream, or the connection is ending; either is noted where it happens.
        }
        catch (IOException failure)
        {
            context.Log.Note($"Stream {stream.Id} ended before its answer was sent: {failure.Message}");
        }
        finally
        {
            streams.TryRemove(stream.Id, out _);
            windows.CloseStream(stream.Id);
            stream.Dispose();
        }
    }

    private async Task<Http2ConnectionError?> AcceptDataAsync(Http2DataFrame data)
    {
        if (data.StreamId > lastStreamId)
        {
            return new(Http2ErrorCode.ProtocolError, $"DATA on stream {data.StreamId}, which was never opened");
        }

        if (data.PayloadLength > 0)
        {
            await frames.WriteAsync(new Http2WindowUpdateFrame(0, data.PayloadLength), ConnectionToken);
        }

        return streams.TryGetValue(data.StreamId, out var stream) ? await AcceptStreamDataAsync(stream, data) : null;
    }

    private async Task<Http2ConnectionError?> AcceptStreamDataAsync(Http2Stream stream, Http2DataFrame data)
    {
        if (!stream.Body.IsStillArriving)
        {
            await ResetStreamAsync(stream.Id, Http2ErrorCode.StreamClosed, "DATA after the stream's END_STREAM");

            return null;
        }

        if (!stream.TakeFromReceiveWindow(data.PayloadLength))
        {
            return new(Http2ErrorCode.FlowControlError, $"DATA on stream {stream.Id} went past the window the server granted");
        }

        var padding = data.PayloadLength - data.Data.Length;
        if (padding > 0)
        {
            await ReplenishStreamAsync(stream, padding);
        }

        if (data.Data.Length > 0)
        {
            stream.Body.Receive(data.Data);
        }

        if (data.EndStream)
        {
            stream.Body.End();
        }

        return null;
    }

    private async Task ReplenishStreamAsync(Http2Stream stream, int increment)
    {
        stream.GiveBackToReceiveWindow(increment);
        await frames.WriteAsync(new Http2WindowUpdateFrame(stream.Id, increment), ConnectionToken);
    }

    private async Task ResetStreamAsync(int streamId, Http2ErrorCode code, string reason)
    {
        context.Log.Note($"Stream {streamId} was reset with {code}: {reason}.");
        if (streams.TryGetValue(streamId, out var stream))
        {
            stream.Reset();
        }

        await frames.WriteAsync(new Http2RstStreamFrame(streamId, code), ConnectionToken);
    }

    // The client closed: the open streams finish what their windows let them send. A
    // connection error: they are cut off, and GOAWAY names the highest stream received.
    private async Task EndAsync(Http2ConnectionError? error)
    {
        windows.StopUpdates();
        foreach (var stream in streams.Values)
        {
            if (error is null)
            {
                stream.Body.Abandon();
            }
            else
            {
                stream.Reset();
            }
        }

        await Task.WhenAll(answering);
        if (error is not null)
        {
            context.Log.Note($"{error.Reason}; answered GOAWAY {error.Code} and closed.");
            await frames.WriteAsync(new Http2GoAwayFrame(lastStreamId, error.Code, []), ConnectionToken);
        }

        await connection.CompleteWritesAsync(ConnectionToken);
    }

    // The exchange was cancelled (ADR-0059): GOAWAY NO_ERROR within its own deadline, which a
    // cancelled exchange token cannot give.
    private async Task WriteFarewellAsync()
    {
        using var deadline = new CancellationTokenSource(FarewellWriteDeadline, context.TimeProvider);
        try
        {
            await frames.WriteAsync(new Http2GoAwayFrame(lastStreamId, Http2ErrorCode.NoError, []), deadline.Token);
            await connection.CompleteWritesAsync(deadline.Token);
            context.Log.Note($"The exchange was cancelled; answered GOAWAY NoError, last stream {lastStreamId}, and closed.");
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            context.Log.Note($"The farewell GOAWAY was not written within its {FarewellWriteDeadline.TotalSeconds}-second write deadline; the connection is closed without it.");
        }
    }
}
