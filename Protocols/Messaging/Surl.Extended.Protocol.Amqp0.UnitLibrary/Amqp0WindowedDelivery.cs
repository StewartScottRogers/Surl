using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// A bus delivery to a consumer that acknowledges, held to its channel's and its connection's
/// <c>basic.qos</c> windows: waiting until both have room, then outstanding in both until settled.
/// </summary>
/// <param name="Channel">The channel the consumer is on.</param>
/// <param name="Consumer">The consumer it is for.</param>
/// <param name="Delivery">The bus's delivery.</param>
internal sealed record Amqp0WindowedDelivery(Amqp0Channel Channel, Amqp0Consumer Consumer, BusQueueDelivery Delivery)
{
    /// <summary>The body's length, which <c>prefetch-size</c> counts.</summary>
    public int BodyBytes => Delivery.Message.Payload.Length;
}
