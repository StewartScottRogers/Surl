using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>One <c>basic.consume</c> on a channel: its tag, whether it acknowledges itself, and its bus consumer.</summary>
/// <param name="tag">The consumer tag.</param>
/// <param name="noAck">Whether each delivery is acknowledged as it is written.</param>
internal sealed class Amqp0Consumer(string tag, bool noAck)
{
    public string Tag { get; } = tag;

    /// <summary>Its entry in the broker's <see cref="Amqp0ConsumerRegistry"/>; set once the consumer is on its channel.</summary>
    public Amqp0ConsumerRegistration Registration { get; set; } = null!;

    public bool NoAck { get; } = noAck;

    /// <summary>The bus consumer; set once the bus has made it, before it is started.</summary>
    public BusQueueConsumer Bus { get; set; } = null!;
}
