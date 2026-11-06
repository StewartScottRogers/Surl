using Surl.Protocol.Abstractions;
using Surl.Qpack;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// Serves HTTP/3 (RFC 9114; ADR-0082 decisions 7 to 9) on one QUIC connection whose handshake has
/// agreed <c>h3</c>: the <see cref="IQuicApplicationProtocol"/> a <see cref="QuicDatagramFlowServer"/>
/// hands the connection to.
/// </summary>
/// <remarks>
/// <para>
/// The server opens its control stream, with <c>SETTINGS</c> first (<see cref="Http3FrameWriter.Settings"/>),
/// and its QPACK encoder and decoder streams, which carry nothing, as nghttp3 does. It accepts the
/// client's: a control stream whose first frame must be <c>SETTINGS</c> (else
/// <c>H3_MISSING_SETTINGS</c>), where a second <c>SETTINGS</c> or a frame not allowed there is
/// <c>H3_FRAME_UNEXPECTED</c>, a repeated or HTTP/2 setting <c>H3_SETTINGS_ERROR</c>, a malformed
/// one <c>H3_FRAME_ERROR</c>, and <c>GOAWAY</c>, <c>MAX_PUSH_ID</c>, <c>CANCEL_PUSH</c>,
/// <c>PRIORITY_UPDATE</c> and unknown types are read and ignored; and its QPACK streams, whose
/// instructions go to <see cref="Http3FieldSections"/>. Any of the three closing is
/// <c>H3_CLOSED_CRITICAL_STREAM</c>, a second of one type or a push stream
/// <c>H3_STREAM_CREATION_ERROR</c>; a stream of an unknown type is stopped with
/// <c>H3_STREAM_CREATION_ERROR</c> and ignored. A frame other than <c>DATA</c> longer than
/// <c>--max-message</c> is <c>H3_EXCESSIVE_LOAD</c>.
/// </para>
/// <para>
/// Each request stream is served on a task of its own by <see cref="Http3RequestStreamServer"/>.
/// A connection error from any stream closes the connection with its code, and the open streams
/// are cut off. When the exchange is cancelled - shutdown (ADR-0059) or <c>--max-time</c> - the
/// server sends <c>GOAWAY</c> naming the next request stream it would have accepted, cuts the open
/// streams off and closes with <c>H3_NO_ERROR</c>. When the client ends the connection the server
/// just stops.
/// </para>
/// <para>
/// With <see cref="Http3ServerOptions.ShutdownGrace"/> set (ADR-0117 decision 6), the cancellation
/// instead marks every extended <c>CONNECT</c> session shutting down and awaits the writes each added
/// with <see cref="Http3ExtendedConnectSession.AddShutdownWrite"/>, then sends <c>GOAWAY</c>, keeps
/// accepting and serving streams until the request streams open at <c>GOAWAY</c> are done or the
/// grace, counted from the cancellation, has passed, and only then cuts the rest off and closes with
/// <c>H3_NO_ERROR</c>.
/// </para>
/// <para>
/// With <see cref="Http3ServerOptions.ExtendedConnectHandlers"/> registered (ADR-0086 decision 3),
/// the server's <c>SETTINGS</c> add <c>SETTINGS_ENABLE_CONNECT_PROTOCOL</c> = 1 and
/// <c>SETTINGS_H3_DATAGRAM</c> = 1, then <see cref="Http3ServerOptions.FurtherSettings"/>; each QUIC
/// <c>DATAGRAM</c> is an HTTP datagram handed to the session its quarter stream ID names (one cut
/// short or past 2^60 - 1 is <c>H3_DATAGRAM_ERROR</c>; one for no open session is held for it, 64 a
/// connection for at most the head timeout, and dropped past either); a handler may close the
/// connection with a code of its own (<see cref="Http3ExtendedConnectSession.CloseConnection"/>); and a
/// client unidirectional stream of a registered type is read as its type and a session ID and
/// handed over, a session ID that is not a client-initiated bidirectional stream ID being
/// <c>H3_ID_ERROR</c>. With none registered, every byte the server sends is as before.
/// </para>
/// </remarks>
/// <param name="handler">Serves every request stream whose request is well formed.</param>
/// <param name="options">The server's options.</param>
/// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
public sealed class Http3ConnectionServer(IHttp3RequestStreamHandler handler, Http3ServerOptions options, ExchangeContext context) : IQuicApplicationProtocol
{
    private const ulong ControlStreamType = 0x00;
    private const ulong PushStreamType = 0x01;
    private const ulong QpackEncoderStreamType = 0x02;
    private const ulong QpackDecoderStreamType = 0x03;

    private readonly IHttp3RequestStreamHandler handler = handler ?? throw new ArgumentNullException(nameof(handler));
    private readonly Http3ServerOptions options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ExchangeContext context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly Http3FieldSections fieldSections = new(context.Limits.MaxMessageBytes);
    private readonly Http3FrameLimit frameLimit = new(context.Limits.MaxMessageBytes);
    private readonly Http3ExtendedConnect extendedConnect = new(options.ExtendedConnectHandlers, context.Log, context.TimeProvider, context.Limits.HeadTimeout);
    private readonly Lock gate = new();
    private readonly List<Task> serving = [];
    private readonly List<Task> requestStreams = [];
    private readonly HashSet<ulong> criticalStreamTypes = [];
    private Http3ConnectionException? error;
    private ulong nextRequestStreamId;
    private ulong? drainedGoAwayStreamId;

    /// <inheritdoc />
    public async Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var failure = new CancellationTokenSource();
        using var graceOver = new CancellationTokenSource();

        // With a shutdown grace, the exchange's cancellation starts DrainAtShutdownAsync, which cuts the streams off once the grace has passed.
        var cutOffAtCancellation = options.ShutdownGrace is null ? context.CancellationToken : CancellationToken.None;
        // The datagram reader leaves out the connection's end, which ends it with no datagram left instead.
        using var stopped = CancellationTokenSource.CreateLinkedTokenSource(cutOffAtCancellation, failure.Token, graceOver.Token);
        using var streamsToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopped.Token);
        var control = await OpenServerStreamsAsync(connection, cancellationToken);
        if (control is not null)
        {
            StartConnectionWork(connection, control, failure, graceOver, stopped.Token, streamsToken.Token);
            await AcceptStreamsAsync(connection, failure, streamsToken.Token);
        }

        await stopped.CancelAsync();
        await Task.WhenAll(TakeServing());
        await EndAsync(connection, control, cancellationToken);
    }

    // Null when the client's transport parameters leave no room for the three streams.
    private async Task<QuicStream?> OpenServerStreamsAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        var control = await OpenServerStreamAsync(connection, [.. QuicVariableLengthInteger.Encode(ControlStreamType), .. Http3FrameWriter.Settings(context.Limits.MaxMessageBytes, [.. extendedConnect.Settings(), .. options.FurtherSettings])], cancellationToken);
        var encoder = await OpenServerStreamAsync(connection, QuicVariableLengthInteger.Encode(QpackEncoderStreamType), cancellationToken);
        var decoder = await OpenServerStreamAsync(connection, QuicVariableLengthInteger.Encode(QpackDecoderStreamType), cancellationToken);
        if (control is null || encoder is null || decoder is null)
        {
            error = new Http3ConnectionException(Http3ErrorCode.GeneralProtocolError, "the client's initial_max_streams_uni leaves no room for the server's control and QPACK streams");
            return null;
        }

        return control;
    }

    private static async Task<QuicStream?> OpenServerStreamAsync(QuicApplicationConnection connection, byte[] opening, CancellationToken cancellationToken)
    {
        var stream = await connection.OpenUnidirectionalStreamAsync(cancellationToken);
        if (stream is not null)
        {
            await connection.WriteAsync(stream, opening, false, cancellationToken);
        }

        return stream;
    }

    // The work beside accepting streams: HTTP datagrams and a handler's close for extended CONNECT, and the drain for a shutdown grace.
    private void StartConnectionWork(QuicApplicationConnection connection, QuicStream control, CancellationTokenSource failure, CancellationTokenSource graceOver, CancellationToken stoppedToken, CancellationToken cancellationToken)
    {
        if (extendedConnect.IsEnabled)
        {
            Start(() => DeliverDatagramsAsync(connection, stoppedToken), failure, stoppedToken);
            Start(() => extendedConnect.ThrowWhenConnectionCloseRequestedAsync(cancellationToken), failure, cancellationToken);
        }

        if (options.ShutdownGrace is { } grace)
        {
            Start(() => DrainAtShutdownAsync(connection, control, grace, failure, graceOver, cancellationToken), failure, cancellationToken);
        }
    }

    // ADR-0117 decision 6: once the exchange is cancelled, the sessions' shutdown writes, then GOAWAY,
    // then the open request streams served until they are done or the grace, counted from the
    // cancellation, has passed.
    private async Task DrainAtShutdownAsync(QuicApplicationConnection connection, QuicStream control, TimeSpan grace, CancellationTokenSource failure, CancellationTokenSource graceOver, CancellationToken cancellationToken)
    {
        await WhenCancelledAsync(context.CancellationToken, cancellationToken);
        using var stopGraceTimer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var graceTimer = Task.Delay(Http3HeadTimeoutDelay.For(grace), context.TimeProvider, stopGraceTimer.Token);

        // The writes are awaited for at most the grace; one still running is cut off with the streams.
        await Task.WhenAny(Start(() => extendedConnect.ShutDownAsync(cancellationToken), failure, cancellationToken), graceTimer);
        ulong goAwayStreamId;
        Task[] open;
        lock (gate)
        {
            goAwayStreamId = nextRequestStreamId;
            open = [.. requestStreams];
        }

        await connection.WriteAsync(control, Http3FrameWriter.GoAway(goAwayStreamId), false, cancellationToken);
        drainedGoAwayStreamId = goAwayStreamId;
        await Task.WhenAny(Task.WhenAll(open), graceTimer);
        await stopGraceTimer.CancelAsync();
        await graceOver.CancelAsync();
    }

    // Completes when awaited is cancelled; throws OperationCanceledException when cancellationToken is first.
    private static async Task WhenCancelledAsync(CancellationToken awaited, CancellationToken cancellationToken)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = awaited.Register(() => cancelled.TrySetResult());
        await cancelled.Task.WaitAsync(cancellationToken);
    }

    private async Task AcceptStreamsAsync(QuicApplicationConnection connection, CancellationTokenSource failure, CancellationToken cancellationToken)
    {
        while (await AcceptStreamOrNullAsync(connection, cancellationToken) is { } stream)
        {
            var reader = new Http3StreamReader(connection, stream);
            if (stream.IsBidirectional)
            {
                extendedConnect.Accept(stream.Id);

                // Numbered and listed in one step under the gate, so the drain's GOAWAY never names a
                // stream it does not wait for; the stream is served only once the gate is let go.
                var listed = new TaskCompletionSource();
                lock (gate)
                {
                    nextRequestStreamId = stream.Id + 4;
                    requestStreams.RemoveAll(task => task.IsCompleted);
                    requestStreams.Add(Start(
                        async () =>
                        {
                            await listed.Task;
                            await new Http3RequestStreamServer(connection, reader, handler, options, context, fieldSections, extendedConnect).ServeAsync(cancellationToken);
                        },
                        failure,
                        cancellationToken));
                }

                listed.SetResult();
            }
            else
            {
                _ = Start(() => ServeUnidirectionalStreamAsync(connection, reader, cancellationToken), failure, cancellationToken);
            }
        }
    }

    // Null once the connection has ended, failed or is shutting down; EndAsync says which.
    private static async Task<QuicStream?> AcceptStreamOrNullAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.AcceptStreamAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private Task Start(Func<Task> work, CancellationTokenSource failure, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            serving.RemoveAll(task => task.IsCompleted);
            var task = RunAsync(work, failure, cancellationToken);
            serving.Add(task);
            return task;
        }
    }

    private Task[] TakeServing()
    {
        lock (gate)
        {
            return [.. serving];
        }
    }

    private async Task RunAsync(Func<Task> work, CancellationTokenSource failure, CancellationToken cancellationToken)
    {
        try
        {
            await work();
        }
        catch (Http3ConnectionException connectionError)
        {
            lock (gate)
            {
                error ??= connectionError;
            }

            await failure.CancelAsync();
        }
        catch (Http3StreamAbandonedException abandoned)
        {
            context.Log.Note(abandoned.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cut off with the rest of the connection's streams.
        }
    }

    // A critical stream that closes is a connection error; any other stream just stops.
    private async Task ServeUnidirectionalStreamAsync(QuicApplicationConnection connection, Http3StreamReader reader, CancellationToken cancellationToken)
    {
        var type = await reader.ReadIntegerOrEndAsync(cancellationToken);
        await (type switch
        {
            null => NoteEndedBeforeType(reader),
            > QpackDecoderStreamType => ServeOtherStreamAsync(connection, reader, type.Value, cancellationToken),
            PushStreamType => throw new Http3ConnectionException(Http3ErrorCode.StreamCreationError, $"the client opened push stream {reader.StreamId}"),
            _ => ServeCriticalStreamAsync(reader, type.Value, cancellationToken), // control, QPACK encoder or QPACK decoder
        });
    }

    // RFC 9297 section 2.1: each QUIC DATAGRAM is a quarter stream ID and a payload, until the connection ends.
    private async Task DeliverDatagramsAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        while (await connection.ReceiveDatagramAsync(cancellationToken) is { } datagram)
        {
            extendedConnect.Deliver(datagram);
        }
    }

    // A registered unidirectional stream type is followed by a session ID and handed over; any other is unknown.
    private async Task ServeOtherStreamAsync(QuicApplicationConnection connection, Http3StreamReader reader, ulong type, CancellationToken cancellationToken)
    {
        if (extendedConnect.HandlerForStreamType(type) is not { } claimant)
        {
            await StopUnknownStreamAsync(connection, reader, type, cancellationToken);
            return;
        }

        var sessionId = await reader.ReadIntegerAsync(cancellationToken);
        await extendedConnect.HandOverAsync(claimant, new Http3SessionStream(connection, reader, sessionId), cancellationToken);
    }

    private Task NoteEndedBeforeType(Http3StreamReader reader)
    {
        context.Log.Note($"Stream {reader.StreamId} ended before its stream type.");
        return Task.CompletedTask;
    }

    // RFC 9114 section 6.2: a stream of an unknown type is stopped and its data discarded.
    private async Task StopUnknownStreamAsync(QuicApplicationConnection connection, Http3StreamReader reader, ulong type, CancellationToken cancellationToken)
    {
        context.Log.Note($"Stream {reader.StreamId} is of unknown type 0x{type:x}; it was stopped with H3_STREAM_CREATION_ERROR.");
        await connection.StopSendingAsync(reader.Stream, (ulong)Http3ErrorCode.StreamCreationError, cancellationToken);
    }

    private async Task ServeCriticalStreamAsync(Http3StreamReader reader, ulong type, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (!criticalStreamTypes.Add(type))
            {
                throw new Http3ConnectionException(Http3ErrorCode.StreamCreationError, $"stream {reader.StreamId} is the client's second stream of type 0x{type:x}");
            }
        }

        try
        {
            await (type == ControlStreamType ? ReadControlStreamAsync(reader, cancellationToken) : ReadQpackStreamAsync(reader, type, cancellationToken));
        }
        catch (Http3StreamAbandonedException) when (reader.Stream.ResetErrorCode is not null)
        {
            // The client reset the stream: it is closed, as a FIN closes it. Abandoned with no
            // reset, the connection ended under it, and RunAsync only notes that.
        }

        throw new Http3ConnectionException(Http3ErrorCode.ClosedCriticalStream, $"the client closed its critical stream {reader.StreamId} of type 0x{type:x}");
    }

    // Returns when the stream ends; every rule it breaks is thrown.
    private async Task ReadControlStreamAsync(Http3StreamReader reader, CancellationToken cancellationToken)
    {
        var hasSettings = false;
        while (await reader.ReadFrameHeaderAsync(cancellationToken) is { } header)
        {
            CheckControlFrame(header, hasSettings, reader.StreamId);
            frameLimit.Check(header, reader.StreamId);
            if (header.Type == Http3FrameType.Settings)
            {
                extendedConnect.ReceiveClientSettings(Http3Settings.Read(await reader.ReadExactlyAsync((int)header.Length, cancellationToken)));
                hasSettings = true;
                continue;
            }

            if (header.Type == Http3FrameType.GoAway)
            {
                context.Log.Note("The client sent GOAWAY.");
            }

            await reader.SkipAsync(header.Length, cancellationToken);
        }
    }

    private static void CheckControlFrame(Http3FrameHeader header, bool hasSettings, ulong streamId)
    {
        if (!hasSettings && header.Type != Http3FrameType.Settings)
        {
            throw new Http3ConnectionException(Http3ErrorCode.MissingSettings, $"the client's control stream {streamId} began with a frame of type 0x{header.Type:x}, not SETTINGS");
        }

        if ((hasSettings && header.Type == Http3FrameType.Settings) || Http3FrameType.IsUnexpectedOnControlStream(header.Type))
        {
            throw new Http3ConnectionException(Http3ErrorCode.FrameUnexpected, $"a frame of type 0x{header.Type:x} arrived on the client's control stream {streamId}");
        }
    }

    private async Task ReadQpackStreamAsync(Http3StreamReader reader, ulong type, CancellationToken cancellationToken)
    {
        var errorCode = type == QpackEncoderStreamType ? Http3ErrorCode.QpackEncoderStreamError : Http3ErrorCode.QpackDecoderStreamError;
        while (await reader.HasMoreBeforeFinAsync(cancellationToken))
        {
            var bytes = await reader.ReadPieceAsync(int.MaxValue, cancellationToken);
            var result = type == QpackEncoderStreamType ? fieldSections.ReceiveEncoderStream(bytes) : fieldSections.ReceiveDecoderStream(bytes);
            if (!result.IsAccepted)
            {
                throw new Http3ConnectionException(errorCode, $"the client's QPACK stream {reader.StreamId} broke a rule: {result.Error}");
            }
        }
    }

    private async Task EndAsync(QuicApplicationConnection connection, QuicStream? control, CancellationToken cancellationToken)
    {
        if (error is not null)
        {
            context.Log.Note($"{error.Message}; closed the HTTP/3 connection with {error.Code}.");
            await connection.CloseAsync((ulong)error.Code, cancellationToken);
            return;
        }

        if (drainedGoAwayStreamId is { } goAwayStreamId)
        {
            context.Log.Note($"The exchange was cancelled; sent GOAWAY {goAwayStreamId}, served the open streams for up to the shutdown grace and closed the HTTP/3 connection with H3_NO_ERROR.");
            await connection.CloseAsync((ulong)Http3ErrorCode.NoError, cancellationToken);
            return;
        }

        // Once the connection has ended the write throws, which tells the flow loop the same.
        if (context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note($"The exchange was cancelled; sent GOAWAY {nextRequestStreamId} and closed the HTTP/3 connection with H3_NO_ERROR.");
            await connection.WriteAsync(control!, Http3FrameWriter.GoAway(nextRequestStreamId), false, cancellationToken);
            await connection.CloseAsync((ulong)Http3ErrorCode.NoError, cancellationToken);
        }
    }
}
