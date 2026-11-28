using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>A bus message handed to a sending link, waiting for credit.</summary>
/// <param name="Message">The message.</param>
/// <param name="Address">The full address its <c>to</c> carries when the container builds its properties.</param>
/// <param name="QueueDeliveryTag">The bus consumer's delivery tag for a delivery from a queue, a kept or non-durable shared subscription's included; <see langword="null"/> only for a live topic subscription's delivery.</param>
/// <param name="Redelivered">Whether the bus marks it redelivered.</param>
internal sealed record Amqp1PendingMessage(BusMessage Message, string Address, long? QueueDeliveryTag, bool Redelivered);
