using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The DNS server over UDP (ADR-0092 decision 3): answers every query a client's flow carries,
/// the one that opened it and each one after, with one datagram each, until the serving engine
/// ends the flow (the idle timeout or the maximum exchange duration, ADR-0092 decision 6).
/// </summary>
/// <remarks>
/// Each query is first offered to the exchange's fault plan (<see cref="DnsQueryFaults"/>), whose
/// chosen RCODE answers it in place of the zones; otherwise it is answered by
/// <see cref="DnsQueryAnswerer"/>. Either way it is written by
/// <see cref="DnsUdpResponseWriter"/>, truncated to the requester's payload size. A datagram the
/// answerer drops - no header, or a response - is noted in the exchange's log and gets nothing.
/// A flow past a connection limit is never created, so there is no refusal to write.
/// </remarks>
public sealed class DnsUdpProtocolServer : IDatagramProtocolServer
{
    private readonly DnsQueryAnswerer answerer;

    /// <summary>
    /// Creates a UDP DNS server that answers from <paramref name="zones"/>, refusing zone transfer
    /// unless <paramref name="allowZoneTransfer"/> (then AXFR is <c>NOTIMP</c> and IXFR the apex
    /// SOA alone, as <see cref="DnsQueryAnswerer.Answer"/> says).
    /// </summary>
    public DnsUdpProtocolServer(DnsZoneSet zones, bool allowZoneTransfer = false)
    {
        answerer = new DnsQueryAnswerer(zones, allowZoneTransfer);
    }

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
        var response = await DnsQueryFaults.FindErrorAsync(context, query) ?? answerer.Answer(query.Span);

        if (response is null)
        {
            context.Log.Note("A datagram with no readable query header, or a response, was dropped unanswered.");
            return;
        }

        await flow.SendAsync(DnsUdpResponseWriter.Write(response), context.CancellationToken);
    }
}
