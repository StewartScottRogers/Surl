using System.Buffers.Binary;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The DNS server over TCP (ADR-0092 decision 4; RFC 1035 section 4.2.2, RFC 7766): reads
/// length-prefixed queries one after another on one persistent connection and answers each as it
/// is read, in arrival order, so a client may pipeline several before reading any answer.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Each answer is written by <see cref="DnsTcpResponseWriter"/>, its length prefix and
/// message in one write, and never truncated below 65535 bytes.</item>
/// <item>Once a length prefix has begun, the rest of its message must arrive within
/// <see cref="ExchangeLimits.HeadTimeout"/>, or the connection closes with no bytes.</item>
/// <item>A length of 0, or above <see cref="ExchangeLimits.MaxMessageBytes"/> when that is set,
/// closes the connection with no bytes; so does a message the answerer drops (no header, or a
/// response).</item>
/// <item>The idle timeout and the maximum exchange duration are the engine's: it cancels the
/// exchange, and the server closes with no bytes and returns. Shutdown ends the call with
/// <see cref="OperationCanceledException"/>.</item>
/// <item>Zone transfer is refused unless allowed; allowed, AXFR and IXFR at a served apex are
/// the whole zone (<see cref="DnsQueryAnswerer.AnswerOverStream"/>).</item>
/// </list>
/// </remarks>
public sealed class DnsTcpProtocolServer : IConnectionProtocolServer
{
    private readonly DnsQueryAnswerer answerer;

    /// <summary>
    /// Creates a TCP DNS server that answers from <paramref name="zones"/>, refusing zone transfer
    /// unless <paramref name="allowZoneTransfer"/>.
    /// </summary>
    public DnsTcpProtocolServer(DnsZoneSet zones, bool allowZoneTransfer = false)
    {
        answerer = new DnsQueryAnswerer(zones, allowZoneTransfer);
    }

    /// <summary>The one scheme answered: <c>dns</c>.</summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["dns"]);

    /// <summary>
    /// Answers every query on <paramref name="connection"/> until the client closes it, a query
    /// closes it as the remarks say, or the engine cancels the exchange.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    /// <exception cref="OperationCanceledException">The engine cancelled the exchange at shutdown.</exception>
    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        var reader = new DnsTcpMessageReader(connection);

        try
        {
            var open = true;
            while (open)
            {
                open = await AnswerNextQueryAsync(reader, connection, context);
            }
        }
        catch (OperationCanceledException) when (context.IsCancelledForALimit)
        {
            context.Log.Note("DNS connection closed: idle timeout or maximum duration");
        }
    }

    // Whether the connection stays open for the next query.
    private async Task<bool> AnswerNextQueryAsync(DnsTcpMessageReader reader, IConnection connection, ExchangeContext context)
    {
        if (!await reader.WaitForByteAsync(context.CancellationToken))
        {
            return false;
        }

        var query = await ReadQueryAsync(reader, context);
        var responses = query is null ? [] : answerer.AnswerOverStream(query);

        if (query is not null && responses.Count == 0)
        {
            context.Log.Note("DNS connection closed: a message with no readable query header, or a response");
        }

        foreach (var response in responses)
        {
            await connection.WriteAsync(DnsTcpResponseWriter.Write(response), context.CancellationToken);
        }

        return responses.Count > 0;
    }

    // The query whose length prefix has begun, read within the head timeout; null when the
    // connection is to close with no bytes.
    private static async Task<byte[]?> ReadQueryAsync(DnsTcpMessageReader reader, ExchangeContext context)
    {
        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);

        try
        {
            var prefix = await reader.ReadExactlyAsync(DnsTcpResponseWriter.LengthPrefixLength, cancellation.Token);
            return prefix is null ? null : await ReadMessageAsync(reader, BinaryPrimitives.ReadUInt16BigEndian(prefix), context, cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            context.Log.Note("DNS connection closed: head timeout");
            return null;
        }
    }

    private static async Task<byte[]?> ReadMessageAsync(DnsTcpMessageReader reader, int length, ExchangeContext context, CancellationToken cancellationToken)
    {
        var maxMessageBytes = context.Limits.MaxMessageBytes;

        if (length == 0 || (maxMessageBytes > 0 && length > maxMessageBytes))
        {
            context.Log.Note($"DNS connection closed: a message length of {length} is 0 or past --max-message {maxMessageBytes}");
            return null;
        }

        return await reader.ReadExactlyAsync(length, cancellationToken);
    }
}
