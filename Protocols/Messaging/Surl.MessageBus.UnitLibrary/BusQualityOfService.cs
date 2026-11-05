namespace Surl.MessageBus;

/// <summary>
/// The quality of service a message was published with (ADR-0100, decision 3.4). It travels
/// with the message; each server delivers with the guarantee its own protocol and client
/// agreed, and across protocols fan-out is at most once.
/// </summary>
public enum BusQualityOfService
{
    /// <summary>At most once: MQTT QoS 0, AMQP 1.0 pre-settled, NATS.</summary>
    AtMostOnce = 0,

    /// <summary>At least once: MQTT QoS 1, unsettled AMQP, STOMP, Kafka.</summary>
    AtLeastOnce = 1,

    /// <summary>Exactly once: MQTT QoS 2.</summary>
    ExactlyOnce = 2,
}
