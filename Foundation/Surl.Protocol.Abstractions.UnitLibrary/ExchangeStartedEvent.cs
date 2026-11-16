using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// An exchange began: a connection was accepted or a datagram flow's first datagram arrived.
/// </summary>
/// <param name="ExchangeId">The exchange's <see cref="ExchangeContext.ExchangeId"/>.</param>
/// <param name="ListenUrl">The listen URL that accepted it, as the instance reports it, bound port included.</param>
/// <param name="Scheme">The listen URL's scheme.</param>
/// <param name="Client">The client's endpoint.</param>
public sealed record ExchangeStartedEvent(long? ExchangeId, string ListenUrl, string Scheme, EndPoint Client) : SurlEvent
{
    /// <inheritdoc />
    public override long? ExchangeId { get; } = ExchangeId;

    /// <inheritdoc />
    public override string? Scheme { get; } = Scheme;

    /// <inheritdoc />
    public override void WriteMembers(ISurlEventMemberWriter writer)
    {
        writer.Number(nameof(ExchangeId), ExchangeId);
        writer.Text(nameof(ListenUrl), ListenUrl);
        writer.Text(nameof(Scheme), Scheme);
        writer.Text(nameof(Client), Client.ToString());
    }
}
