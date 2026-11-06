using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// A delivery the client has not settled: from a consumer, or from <c>basic.get</c> when
/// <paramref name="Consumer"/> is <see langword="null"/>.
/// </summary>
/// <param name="Consumer">The bus consumer that delivered it; <see langword="null"/> for a get.</param>
/// <param name="QueueName">The queue it came from.</param>
/// <param name="BusDeliveryTag">The bus's delivery tag for it.</param>
internal sealed record Amqp0OutstandingDelivery(BusQueueConsumer? Consumer, string QueueName, long BusDeliveryTag);
