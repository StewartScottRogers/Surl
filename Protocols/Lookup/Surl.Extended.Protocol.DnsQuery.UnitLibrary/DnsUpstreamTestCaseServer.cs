using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The DNS server half of upstream curl's test cases over UDP (ADR-0123 decision 1): logs each
/// query in <see cref="DnsQueryUpstreamTestCaseReplies"/>, then answers it from the case's
/// zones exactly as <see cref="DnsUdpProtocolServer"/> does. Never composed by the shipped
/// <c>surl</c>.
/// </summary>
/// <param name="replies">The case's <c>&lt;dns&gt;</c> section.</param>
public sealed class DnsUpstreamTestCaseServer(DnsQueryUpstreamTestCaseReplies replies) : IDatagramProtocolServer
{
    private readonly DnsQueryAnswerer answerer = new(replies.Zones);

    /// <summary>The one scheme answered: <c>dns</c>.</summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["dns"]);

    /// <summary>
    /// Answers the query that opened <paramref name="flow"/> and every later one, until the
    /// exchange is cancelled, which ends the call with <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="flow">The flow opened by its first datagram.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public async Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(context);

        var query = flow.FirstDatagram;

        while (true)
        {
            await AnswerAsync(flow, query, context);
            query = await flow.ReceiveAsync(context.CancellationToken);
        }
    }

    private async Task AnswerAsync(IDatagramFlow flow, ReadOnlyMemory<byte> query, ExchangeContext context)
    {
        if (DnsQueryAnswerer.StandardQueryOf(query.Span) is { } standard)
        {
            replies.RecordQuery(standard.Question);
        }

        if (answerer.Answer(query.Span) is { } response)
        {
            await flow.SendAsync(DnsUdpResponseWriter.Write(response), context.CancellationToken);
        }
    }
}
