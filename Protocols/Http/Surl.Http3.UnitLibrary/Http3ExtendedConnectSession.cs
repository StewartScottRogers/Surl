using System.Threading.Channels;
using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// One extended <c>CONNECT</c> session (RFC 9220, RFC 9297; ADR-0086 decision 3), as its
/// <see cref="IHttp3ExtendedConnectHandler"/> sees it: the request stream that opened it, the
/// capsules on that stream, the HTTP datagrams whose quarter stream ID names it, and the streams
/// whose header names it.
/// </summary>
/// <remarks>
/// <para>
/// The handler answers with <see cref="RequestStream"/>'s <c>WriteHeadersAsync</c> (a 2xx accepts
/// the session) and ends the session with its <c>FIN</c> or a reset. Capsules travel in the request
/// stream's <c>DATA</c> frames (RFC 9297 section 3), so a handler reads them with
/// <see cref="ReadCapsuleAsync"/>, never with the stream's <c>ReadBodyAsync</c>.
/// </para>
/// <para>
/// The session is open from the moment its <c>CONNECT</c> reaches the handler until
/// <see cref="IHttp3ExtendedConnectHandler.ServeSessionAsync"/> returns; datagrams and streams that
/// arrive in that time wait here, whether or not the handler has answered yet. After it, datagrams
/// for the session are dropped (RFC 9297 section 2.1) and streams naming it go to
/// <see cref="IHttp3ExtendedConnectHandler.ServeStreamWithoutSessionAsync"/>.
/// </para>
/// </remarks>
public sealed class Http3ExtendedConnectSession
{
    /// <summary>How many received datagrams wait for the handler; the oldest are dropped past it (RFC 9297 section 2.1 lets a receiver drop them).</summary>
    public const int ReceivedDatagramCapacity = 128;

    private readonly QuicApplicationConnection connection;
    private readonly Http3ExtendedConnect extendedConnect;
    private readonly Http3FrameLimit frameLimit;
    private readonly Channel<byte[]> datagrams = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(ReceivedDatagramCapacity) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<Http3SessionStream> streams = Channel.CreateUnbounded<Http3SessionStream>();
    private byte[] capsuleBytes = [];

    /// <summary>Opens the session the extended <c>CONNECT</c> on <paramref name="requestStream"/> asks for.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="requestStream">The <c>CONNECT</c>'s request stream.</param>
    /// <param name="handler">The handler registered for its <c>:protocol</c>.</param>
    /// <param name="extendedConnect">The connection's registrations and open sessions.</param>
    /// <param name="frameLimit">The longest capsule, as the longest frame other than <c>DATA</c>.</param>
    internal Http3ExtendedConnectSession(
        QuicApplicationConnection connection,
        Http3RequestStream requestStream,
        IHttp3ExtendedConnectHandler handler,
        Http3ExtendedConnect extendedConnect,
        Http3FrameLimit frameLimit)
    {
        this.connection = connection;
        this.extendedConnect = extendedConnect;
        this.frameLimit = frameLimit;
        RequestStream = requestStream;
        Handler = handler;
    }

    /// <summary>The session ID: the stream ID of its <c>CONNECT</c> request stream.</summary>
    public ulong Id => RequestStream.Id;

    /// <summary>The <c>CONNECT</c>'s head; <see cref="Http3RequestHead.Protocol"/> is the handler's <c>:protocol</c>.</summary>
    public Http3RequestHead Head => RequestStream.Head;

    /// <summary>The <c>CONNECT</c>'s request stream, to answer the request and to end the session.</summary>
    public Http3RequestStream RequestStream { get; }

    /// <summary>The handler serving the session.</summary>
    internal IHttp3ExtendedConnectHandler Handler { get; }

    /// <summary>
    /// The client's <c>SETTINGS</c>, identifier to value, once its control stream has delivered them;
    /// a session's protocol may require some before it serves the session (RFC 9297 section 2.1.1).
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The client's settings.</returns>
    public Task<IReadOnlyDictionary<ulong, ulong>> ReadClientSettingsAsync(CancellationToken cancellationToken) =>
        extendedConnect.ReadClientSettingsAsync(cancellationToken);

    /// <summary>
    /// Reads the next capsule from the request stream's <c>DATA</c> frames, of any type: unknown
    /// types are the handler's to drop (RFC 9297 section 3.2). A capsule whose length is past
    /// <c>--max-message</c> resets the stream <c>H3_EXCESSIVE_LOAD</c>, and a stream that ends part
    /// way through a capsule is malformed (section 3.3) and reset <c>H3_MESSAGE_ERROR</c>; both
    /// return <see langword="null"/>, as the stream's end does.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The capsule, or <see langword="null"/> once the stream has ended or been reset.</returns>
    public async Task<Http3Capsule?> ReadCapsuleAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (ReadCapsuleHeader() is { } header)
            {
                if (header.Length > (ulong)frameLimit.MaxLength)
                {
                    await RequestStream.ResetAsync(Http3ErrorCode.ExcessiveLoad, $"a capsule of {header.Length} bytes is past the {frameLimit.MaxLength}-byte message limit", cancellationToken);
                    return null;
                }

                if ((ulong)(capsuleBytes.Length - header.HeaderLength) >= header.Length)
                {
                    return TakeCapsule(header);
                }
            }

            if (await RequestStream.ReadBodyAsync(cancellationToken) is not { } piece)
            {
                return await EndCapsulesAsync(cancellationToken);
            }

            capsuleBytes = [.. capsuleBytes, .. piece];
        }
    }

    /// <summary>Writes one capsule on the request stream, in one <c>DATA</c> frame.</summary>
    /// <param name="capsule">The capsule.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the frame is written.</returns>
    public async Task WriteCapsuleAsync(Http3Capsule capsule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capsule);
        await RequestStream.WriteDataAsync(
            (byte[])[.. QuicVariableLengthInteger.Encode(capsule.Type), .. QuicVariableLengthInteger.Encode((ulong)capsule.Value.Length), .. capsule.Value],
            endStream: false,
            cancellationToken);
    }

    /// <summary>Waits for the payload of the next HTTP datagram whose quarter stream ID names this session (RFC 9297 section 2.1).</summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The payload, without the quarter stream ID, or <see langword="null"/> once the session has ended and none is left.</returns>
    public async ValueTask<byte[]?> ReceiveDatagramAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await datagrams.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    /// <summary>Sends <paramref name="payload"/> as an HTTP datagram of this session: one QUIC <c>DATAGRAM</c> frame, the quarter stream ID first.</summary>
    /// <param name="payload">The payload.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the frame is sent.</returns>
    /// <exception cref="InvalidOperationException">The client's <c>max_datagram_frame_size</c> is absent or 0.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The frame would be larger than the client takes or than one packet holds.</exception>
    public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
        connection.SendDatagramAsync((byte[])[.. QuicVariableLengthInteger.Encode(Id / 4), .. payload.Span], cancellationToken);

    /// <summary>Waits for the next client stream whose header names this session.</summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The stream, or <see langword="null"/> once the session has ended and none is left.</returns>
    public async ValueTask<Http3SessionStream?> AcceptStreamAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await streams.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Opens a server unidirectional stream for this session and writes its header: the handler's
    /// <see cref="IHttp3ExtendedConnectHandler.UnidirectionalStreamType"/>, then the session ID.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The stream, or <see langword="null"/> when the client's stream limit allows no more now.</returns>
    /// <exception cref="InvalidOperationException">The handler registered no unidirectional stream type.</exception>
    public async Task<Http3SessionStream?> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken)
    {
        var type = Handler.UnidirectionalStreamType ?? throw new InvalidOperationException($"The handler for {Handler.Protocol} registered no unidirectional stream type.");
        if (await connection.OpenUnidirectionalStreamAsync(cancellationToken) is not { } stream)
        {
            return null;
        }

        await connection.WriteAsync(stream, (byte[])[.. QuicVariableLengthInteger.Encode(type), .. QuicVariableLengthInteger.Encode(Id)], false, cancellationToken);
        return new Http3SessionStream(connection, new Http3StreamReader(connection, stream), Id);
    }

    /// <summary>Hands the session a datagram's payload.</summary>
    /// <param name="payload">The payload, without the quarter stream ID.</param>
    internal void Deliver(byte[] payload) => datagrams.Writer.TryWrite(payload);

    /// <summary>Hands the session a stream whose header names it.</summary>
    /// <param name="stream">The stream.</param>
    internal void Deliver(Http3SessionStream stream) => streams.Writer.TryWrite(stream);

    /// <summary>Ends the session: what waits can still be taken, then the waits return <see langword="null"/>.</summary>
    internal void End()
    {
        datagrams.Writer.TryComplete();
        streams.Writer.TryComplete();
    }

    // The next capsule's type and length, once both have arrived.
    private (ulong Type, ulong Length, int HeaderLength)? ReadCapsuleHeader()
    {
        var bytes = capsuleBytes.AsSpan();
        return QuicVariableLengthInteger.TryRead(bytes, out var type, out var typeLength)
            && QuicVariableLengthInteger.TryRead(bytes[typeLength..], out var length, out var lengthLength)
            ? (type, length, typeLength + lengthLength)
            : null;
    }

    private Http3Capsule TakeCapsule((ulong Type, ulong Length, int HeaderLength) header)
    {
        var end = header.HeaderLength + (int)header.Length;
        var capsule = new Http3Capsule(header.Type, capsuleBytes[header.HeaderLength..end]);
        capsuleBytes = capsuleBytes[end..];
        return capsule;
    }

    private async Task<Http3Capsule?> EndCapsulesAsync(CancellationToken cancellationToken)
    {
        if (capsuleBytes.Length > 0 && !RequestStream.HasRequestBodyEndedEarly)
        {
            await RequestStream.ResetAsync(Http3ErrorCode.MessageError, "it ended part way through a capsule", cancellationToken);
        }

        return null;
    }
}
