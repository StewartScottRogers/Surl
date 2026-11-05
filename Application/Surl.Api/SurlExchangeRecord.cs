using System.Net;

namespace Surl.Api;

/// <summary>
/// One exchange a <see cref="SurlServer"/> served with <see cref="SurlServerOptions.RecordExchanges"/>
/// on (ADR-0099, decision 7): who the client was, when the exchange started, and the bytes it
/// received and sent in the order they crossed the connection.
/// </summary>
/// <param name="ExchangeId">The exchange's ID, as the exchange log numbers it.</param>
/// <param name="Client">The client's endpoint.</param>
/// <param name="StartedAt">When the exchange started, from <see cref="SurlServerOptions.TimeProvider"/>.</param>
/// <param name="Chunks">The bytes in the order they crossed the connection.</param>
/// <param name="IsTruncated">
/// Whether the exchange carried more than <see cref="SurlServerOptions.MaxRecordedBytesPerExchange"/>
/// bytes, both directions together, so the bytes past that cap were not kept.
/// </param>
public sealed record SurlExchangeRecord(
    long ExchangeId,
    EndPoint Client,
    DateTimeOffset StartedAt,
    IReadOnlyList<SurlExchangeChunk> Chunks,
    bool IsTruncated)
{
    /// <summary>Every <see cref="SurlExchangeDirection.Received"/> chunk's bytes, concatenated in order.</summary>
    public byte[] Received => Concatenate(SurlExchangeDirection.Received);

    /// <summary>Every <see cref="SurlExchangeDirection.Sent"/> chunk's bytes, concatenated in order.</summary>
    public byte[] Sent => Concatenate(SurlExchangeDirection.Sent);

    private byte[] Concatenate(SurlExchangeDirection direction) =>
        [.. Chunks.Where(chunk => chunk.Direction == direction).SelectMany(chunk => chunk.Bytes.ToArray())];
}
