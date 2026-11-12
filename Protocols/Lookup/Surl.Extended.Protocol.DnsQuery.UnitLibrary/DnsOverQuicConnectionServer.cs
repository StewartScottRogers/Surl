using System.Buffers.Binary;
using System.Collections.Concurrent;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Serves DNS over QUIC (RFC 9250; ADR-0092 decision 5) on one QUIC connection whose handshake
/// has agreed <c>doq</c>: the <see cref="IQuicApplicationProtocol"/> a
/// <see cref="QuicDatagramFlowServer"/> hands the connection to.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Each query arrives on its own client-initiated bidirectional stream, as a two-byte length
/// and the message (section 4.2). Once the whole message has arrived it is answered on that stream,
/// each response length-prefixed (more than one only for an allowed zone transfer, section 4.2.2),
/// and the stream is ended with FIN; the server does not wait for the client's FIN first.</item>
/// <item>Streams are served side by side, each on a task of its own.</item>
/// <item>Each query is first offered to the exchange's fault plan (<see cref="DnsQueryFaults"/>), whose
/// chosen RCODE answers it in place of the zones.</item>
/// <item>These close the connection with <see cref="DnsOverQuicErrorCode.ProtocolError"/>: a
/// Message ID other than 0 (section 4.2.1); a client-initiated unidirectional stream; a length
/// prefix of 0, past <see cref="ExchangeLimits.MaxMessageBytes"/> when that is set, or longer than
/// the bytes before FIN; a message the answerer drops (no header, or a response); and any byte
/// after the message (section 4.3.3).</item>
/// <item>A stream the client resets before its query is whole is dropped without an answer
/// (section 4.3.1). A stream whose client never sends FIN after its query is answered all the
/// same, and its receiving half is left open until the connection ends.</item>
/// <item>A fault of the server's own closes the connection with
/// <see cref="DnsOverQuicErrorCode.InternalError"/>.</item>
/// <item>When the exchange is cancelled - shutdown, the idle timeout or the maximum duration - the
/// open streams are cut off and the server returns, which closes the connection with
/// <see cref="DnsOverQuicErrorCode.NoError"/>; so it does when the client ends the connection.</item>
/// <item>How many streams may be open at once is QUIC's <c>initial_max_streams_bidi</c>: one past it
/// is <c>Surl.Quic</c>'s <c>STREAM_LIMIT_ERROR</c>, never seen here.</item>
/// </list>
/// </remarks>
public sealed class DnsOverQuicConnectionServer : IQuicApplicationProtocol
{
    private const int LengthPrefixLength = 2;

    private readonly Func<byte[], IReadOnlyList<DnsResponse>> answer;
    private readonly ExchangeContext context;
    private readonly ConcurrentQueue<(DnsOverQuicErrorCode Code, string Reason)> failures = new();

    /// <summary>Creates a server that answers every query with <paramref name="answerer"/>.</summary>
    /// <param name="answerer">Answers each query, as over a stream, so an allowed zone transfer is whole.</param>
    /// <param name="context">The exchange: its limits, log and cancellation.</param>
    public DnsOverQuicConnectionServer(DnsQueryAnswerer answerer, ExchangeContext context)
        : this(AnswerOverStream(answerer), context)
    {
    }

    /// <summary>Creates a server that answers every query with <paramref name="answer"/>, which returns no response for a query it drops.</summary>
    internal DnsOverQuicConnectionServer(Func<byte[], IReadOnlyList<DnsResponse>> answer, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.answer = answer;
        this.context = context;
    }

    /// <inheritdoc />
    public async Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var failure = new CancellationTokenSource();
        using var streams = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, context.CancellationToken, failure.Token);
        var serving = new List<Task>();
        while (await AcceptStreamOrNullAsync(connection, streams.Token) is { } stream)
        {
            serving.RemoveAll(task => task.IsCompleted);
            serving.Add(ServeStreamAsync(connection, stream, failure, streams.Token));
        }

        await streams.CancelAsync();
        await Task.WhenAll(serving);
        if (failures.TryPeek(out var first))
        {
            context.Log.Note($"DNS over QUIC connection closed with {first.Code}: {first.Reason}");
            await connection.CloseAsync((ulong)first.Code, cancellationToken);
        }
    }

    private static Func<byte[], IReadOnlyList<DnsResponse>> AnswerOverStream(DnsQueryAnswerer answerer)
    {
        ArgumentNullException.ThrowIfNull(answerer);
        return query => answerer.AnswerOverStream(query);
    }

    // Null once the connection has ended, a stream failed it, or the exchange was cancelled.
    private static async Task<QuicStream?> AcceptStreamOrNullAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.AcceptStreamAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async Task ServeStreamAsync(QuicApplicationConnection connection, QuicStream stream, CancellationTokenSource failure, CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            await AnswerStreamAsync(connection, stream, cancellationToken);
        }
        catch (DnsOverQuicProtocolException violation)
        {
            await FailAsync(DnsOverQuicErrorCode.ProtocolError, violation.Message, failure);
        }
        catch (OperationCanceledException)
        {
            // Cut off: the connection ended, another stream failed it, or the exchange was cancelled.
        }
        catch (Exception fault)
        {
            await FailAsync(DnsOverQuicErrorCode.InternalError, $"stream {stream.Id} failed: {fault.Message}", failure);
        }
    }

    // The first failure is the one the connection closes with; any later one, cut off by it, is not.
    private async Task FailAsync(DnsOverQuicErrorCode code, string reason, CancellationTokenSource failure)
    {
        failures.Enqueue((code, reason));
        await failure.CancelAsync();
    }

    private async Task AnswerStreamAsync(QuicApplicationConnection connection, QuicStream stream, CancellationToken cancellationToken)
    {
        if (!stream.IsBidirectional)
        {
            throw new DnsOverQuicProtocolException($"the client opened unidirectional stream {stream.Id}");
        }

        var query = await ReadQueryAsync(connection, stream, cancellationToken);
        if (query is null)
        {
            context.Log.Note($"DNS over QUIC stream {stream.Id} ended without its query whole (reset by the client, or the connection ended); dropped");
            return;
        }

        var responses = await AnswerAsync(query, stream.Id);
        for (var index = 0; index < responses.Count; index++)
        {
            await connection.WriteAsync(stream, DnsTcpResponseWriter.WritePadded(responses[index]), index == responses.Count - 1, cancellationToken);
        }

        if (await connection.ReadAsync(stream, new byte[1], cancellationToken) > 0)
        {
            throw new DnsOverQuicProtocolException($"stream {stream.Id} carried bytes after its query");
        }
    }

    // The query's message; null when the client reset the stream, or the connection ended, first.
    private async Task<byte[]?> ReadQueryAsync(QuicApplicationConnection connection, QuicStream stream, CancellationToken cancellationToken)
    {
        var prefix = await ReadExactlyAsync(connection, stream, LengthPrefixLength, cancellationToken);
        if (prefix is null)
        {
            return null;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(prefix);
        var maxMessageBytes = context.Limits.MaxMessageBytes;
        if (length == 0 || (maxMessageBytes > 0 && length > maxMessageBytes))
        {
            throw new DnsOverQuicProtocolException($"stream {stream.Id} has a message length of {length}, 0 or past --max-message {maxMessageBytes}");
        }

        return await ReadExactlyAsync(connection, stream, length, cancellationToken);
    }

    // Null when the client reset the stream, or the connection ended, before the bytes came; FIN
    // before them is a violation.
    private static async Task<byte[]?> ReadExactlyAsync(QuicApplicationConnection connection, QuicStream stream, int length, CancellationToken cancellationToken)
    {
        var bytes = new byte[length];
        for (var filled = 0; filled < length;)
        {
            var count = await connection.ReadAsync(stream, bytes.AsMemory(filled), cancellationToken);
            if (count == 0)
            {
                return EndedEarly(stream, filled, length);
            }

            filled += count;
        }

        return bytes;
    }

    private static byte[]? EndedEarly(QuicStream stream, int filled, int length) =>
        stream.IsReadComplete
            ? throw new DnsOverQuicProtocolException($"stream {stream.Id} ended after {filled} of {length} bytes")
            : null;

    private async Task<IReadOnlyList<DnsResponse>> AnswerAsync(byte[] query, ulong streamId)
    {
        if (query.Length >= LengthPrefixLength && BinaryPrimitives.ReadUInt16BigEndian(query) != 0)
        {
            throw new DnsOverQuicProtocolException($"stream {streamId} carried a query with Message ID {BinaryPrimitives.ReadUInt16BigEndian(query)}, not 0");
        }

        if (await DnsQueryFaults.FindErrorAsync(context, query) is { } chosen)
        {
            return [chosen];
        }

        var responses = answer(query);
        return responses.Count > 0
            ? responses
            : throw new DnsOverQuicProtocolException($"stream {streamId} carried a message with no readable query header, or a response");
    }
}
