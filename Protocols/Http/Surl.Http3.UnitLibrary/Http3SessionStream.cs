using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// A raw QUIC stream that belongs to an extended <c>CONNECT</c> session (ADR-0086 decision 3): a
/// client stream read past its signal or type and its session ID, or one the server opened through
/// <see cref="Http3ExtendedConnectSession.OpenUnidirectionalStreamAsync"/> with both already written.
/// </summary>
public sealed class Http3SessionStream
{
    private readonly QuicApplicationConnection connection;
    private readonly Http3StreamReader reader;

    /// <summary>Wraps the stream <paramref name="reader"/> reads, past its header.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="reader">The stream's reader, which may hold bytes already received after the header.</param>
    /// <param name="sessionId">The session ID the stream's header names.</param>
    internal Http3SessionStream(QuicApplicationConnection connection, Http3StreamReader reader, ulong sessionId)
    {
        this.connection = connection;
        this.reader = reader;
        SessionId = sessionId;
    }

    /// <summary>The QUIC stream ID.</summary>
    public ulong Id => reader.StreamId;

    /// <summary>The session ID the stream's header names: the stream ID of the session's <c>CONNECT</c>.</summary>
    public ulong SessionId { get; }

    /// <summary>The QUIC stream, for its direction, reset and <c>STOP_SENDING</c> codes.</summary>
    public QuicStream Stream => reader.Stream;

    /// <summary>Reads what has arrived after the header, waiting for at least one byte.</summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>How many bytes were copied; 0 once the stream has ended (its <c>FIN</c>, a reset, or the connection's end).</returns>
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => reader.ReadAsync(buffer, cancellationToken);

    /// <summary>Writes <paramref name="data"/> on the stream, with its <c>FIN</c> when <paramref name="fin"/>.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="fin">Whether the write ends the server's half.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, bool fin, CancellationToken cancellationToken) =>
        connection.WriteAsync(reader.Stream, data, fin, cancellationToken);

    /// <summary>Ends the server's half with <c>RESET_STREAM</c> <paramref name="applicationErrorCode"/>.</summary>
    /// <param name="applicationErrorCode">The error code.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the frame is queued.</returns>
    public ValueTask ResetAsync(ulong applicationErrorCode, CancellationToken cancellationToken) =>
        connection.ResetAsync(reader.Stream, applicationErrorCode, cancellationToken);

    /// <summary>Ends the server's half with <c>RESET_STREAM_AT</c>, delivering the first <paramref name="reliableSize"/> bytes.</summary>
    /// <param name="applicationErrorCode">The error code.</param>
    /// <param name="reliableSize">How many bytes of the server's half must still be delivered.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the frame is queued.</returns>
    public ValueTask ResetAtAsync(ulong applicationErrorCode, ulong reliableSize, CancellationToken cancellationToken) =>
        connection.ResetAtAsync(reader.Stream, applicationErrorCode, reliableSize, cancellationToken);

    /// <summary>Asks the client to stop sending with <c>STOP_SENDING</c> <paramref name="applicationErrorCode"/>.</summary>
    /// <param name="applicationErrorCode">The error code.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes when the frame is queued.</returns>
    public ValueTask StopSendingAsync(ulong applicationErrorCode, CancellationToken cancellationToken) =>
        connection.StopSendingAsync(reader.Stream, applicationErrorCode, cancellationToken);
}
