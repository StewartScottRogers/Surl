namespace Surl.Extended.Protocol.Amqp0;

/// <summary>A durable declared exchange as <see cref="Amqp0ExchangeFiles"/> keeps it: its declaration and bindings.</summary>
/// <param name="Name">The exchange's name.</param>
/// <param name="Type">Its type.</param>
/// <param name="Arguments">Its declared arguments, encoded as a field table.</param>
/// <param name="Bindings">Its bindings.</param>
internal sealed record Amqp0PersistedExchange(string Name, Amqp0ExchangeType Type, byte[] Arguments, IReadOnlyList<Amqp0Binding> Bindings);
