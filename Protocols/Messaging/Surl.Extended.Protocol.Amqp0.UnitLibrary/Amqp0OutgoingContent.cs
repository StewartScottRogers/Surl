namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// What <c>basic.deliver</c>, <c>basic.get-ok</c> or <c>basic.return</c> carries for one message:
/// its exchange and routing key, its content header's property flags and list, and its body.
/// </summary>
/// <param name="Exchange">The exchange it was published to.</param>
/// <param name="RoutingKey">Its routing key.</param>
/// <param name="PropertyFlags">The content header's property flags.</param>
/// <param name="PropertyList">The content header's property list.</param>
/// <param name="Body">The body.</param>
internal sealed record Amqp0OutgoingContent(string Exchange, string RoutingKey, ushort PropertyFlags, ReadOnlyMemory<byte> PropertyList, ReadOnlyMemory<byte> Body);
