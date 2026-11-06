namespace Surl.MessageBus;

/// <summary>
/// One <c>--topic-map &lt;scheme&gt;:&lt;name&gt;=&lt;scheme&gt;:&lt;name&gt;</c> (ADR-0102,
/// decision 3.3): on <see cref="Scheme"/> and its TLS twin, the exact name <see cref="Name"/>
/// means the canonical topic <see cref="TargetName"/> has on <see cref="TargetScheme"/> by the
/// default mapping, both ways. <see cref="TopicNameLookup"/> checks both names when it is made.
/// </summary>
/// <param name="Scheme">The scheme whose name is aliased, such as <c>amqp</c>.</param>
/// <param name="Name">The exact name on that scheme, such as <c>orders</c>.</param>
/// <param name="TargetScheme">The scheme whose default mapping resolves <paramref name="TargetName"/>.</param>
/// <param name="TargetName">The name on the target scheme, such as <c>shop/orders</c>.</param>
public sealed record TopicMap(string Scheme, string Name, string TargetScheme, string TargetName);
