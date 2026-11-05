using Surl.HttpServing;
using Surl.Protocol.Abstractions;
using Surl.Qpack;
using Surl.Quic;

namespace Surl.Protocol.Http;

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
/// </remarks>
/// <param name="handler">Decides the answer to every well-formed request.</param>
/// <param name="context">The exchange: its clock, limits, log and cancellation.</param>
internal sealed class Http3ConnectionServer(IHttpRequestHandler handler, ExchangeContext context) : IQuicApplicationProtocol
{
    private const ulong ControlStreamType = 0x00;
    private const ulong PushStreamType = 0x01;
    private const ulong QpackEncoderStreamType = 0x02;
    private const ulong QpackDecoderStreamType = 0x03;

    private readonly Http3FieldSections fieldSections = new(context.Limits.MaxMessageBytes);
    private readonly Http3FrameLimit frameLimit = new(context.Limits.MaxMessageBytes);
    private readonly Lock gate = new();
    private readonly List<Task> serving = [];
    private readonly HashSet<ulong> criticalStreamTypes = [];
    private Http3ConnectionException? error;
    private ulong nextRequestStreamId;

    /// <inheritdoc />
    public async Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var failure = new CancellationTokenSource();
        using var streamsToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, context.CancellationToken, failure.Token);
        var control = await OpenServerStreamsAsync(connection, cancellationToken);
        if (control is not null)
        {
            await AcceptStreamsAsync(connection, failure, streamsToken.Token);
        }

        await streamsToken.CancelAsync();
        await Task.WhenAll(TakeServing());
        await EndAsync(connection, control, cancellationToken);
    }

    // Null when the client's transport parameters leave no room for the three streams.
    private async Task<QuicStream?> OpenServerStreamsAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        var control = await OpenServerStreamAsync(connection, [.. QuicVariableLengthInteger.Encode(ControlStreamType), .. Http3FrameWriter.Settings(context.Limits.MaxMessageBytes)], cancellationToken);
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

    private async Task AcceptStreamsAsync(QuicApplicationConnection connection, CancellationTokenSource failure, CancellationToken cancellationToken)
    {
        while (await AcceptStreamOrNullAsync(connection, cancellationToken) is { } stream)
        {
            var reader = new Http3StreamReader(connection, stream);
            if (stream.IsBidirectional)
            {
                nextRequestStreamId = stream.Id + 4;
                Start(() => new Http3RequestStreamServer(connection, reader, handler, context, fieldSections).ServeAsync(cancellationToken), failure, cancellationToken);
            }
            else
            {
                Start(() => ServeUnidirectionalStreamAsync(connection, reader, cancellationToken), failure, cancellationToken);
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

    private void Start(Func<Task> work, CancellationTokenSource failure, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            serving.RemoveAll(task => task.IsCompleted);
            serving.Add(RunAsync(work, failure, cancellationToken));
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
            > QpackDecoderStreamType => StopUnknownStreamAsync(connection, reader, type.Value, cancellationToken),
            PushStreamType => throw new Http3ConnectionException(Http3ErrorCode.StreamCreationError, $"the client opened push stream {reader.StreamId}"),
            _ => ServeCriticalStreamAsync(reader, type.Value, cancellationToken), // control, QPACK encoder or QPACK decoder
        });
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
                Http3Settings.Validate(await reader.ReadExactlyAsync((int)header.Length, cancellationToken));
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

        // Once the connection has ended the write throws, which tells the flow loop the same.
        if (context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note($"The exchange was cancelled; sent GOAWAY {nextRequestStreamId} and closed the HTTP/3 connection with H3_NO_ERROR.");
            await connection.WriteAsync(control!, Http3FrameWriter.GoAway(nextRequestStreamId), false, cancellationToken);
            await connection.CloseAsync((ulong)Http3ErrorCode.NoError, cancellationToken);
        }
    }
}
