namespace Surl.Extended.Protocol.Amqp0;

/// <summary>The exchange types the broker serves (section 3.1.3; ADR-0112 decision 5).</summary>
internal enum Amqp0ExchangeType
{
    /// <summary><c>direct</c>: queues bound with a key equal to the routing key.</summary>
    Direct,

    /// <summary><c>fanout</c>: every bound queue.</summary>
    Fanout,

    /// <summary><c>topic</c>: queues bound with a key whose words match the routing key's.</summary>
    Topic,

    /// <summary><c>headers</c>: queues whose binding arguments match the message's <c>headers</c> table.</summary>
    Headers,
}
