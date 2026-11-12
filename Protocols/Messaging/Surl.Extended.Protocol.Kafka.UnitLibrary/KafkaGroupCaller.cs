using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Who made a consumer group request, as <see cref="KafkaGroupCoordinator"/> records it: the
/// exchange its events name, the scheme, the account, where its events are
/// published and where a new member id's bytes come from.
/// </summary>
/// <param name="ExchangeId">The exchange the request came on.</param>
/// <param name="Scheme">The scheme the request came on (<c>kafka</c> or <c>kafkas</c>).</param>
/// <param name="Account">The caller's account; null for an anonymous one.</param>
/// <param name="Events">Where the group's changes are published (<see cref="ExchangeContext.Events"/>).</param>
/// <param name="Identifiers">Where a new member id's 16 bytes come from (<see cref="ExchangeContext.Identifiers"/>).</param>
internal sealed record KafkaGroupCaller(
    long ExchangeId,
    string Scheme,
    string? Account,
    ISurlEventPublisher Events,
    IIdentifierRandom Identifiers)
{
    /// <summary>The caller of <paramref name="request"/> on the exchange <paramref name="context"/>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="context">The exchange's context.</param>
    /// <returns>The caller.</returns>
    public static KafkaGroupCaller Of(KafkaRequest request, ExchangeContext context) => new(
        context.ExchangeId,
        context.Scheme,
        request.Connection.AccountName,
        context.Events,
        context.Identifiers);
}
