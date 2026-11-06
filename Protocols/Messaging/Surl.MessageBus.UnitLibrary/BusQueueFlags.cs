namespace Surl.MessageBus;

/// <summary>
/// The flags a named queue is declared with (ADR-0100, decision 3.3). A queue declared again
/// must be declared with the same flags, or the declare is refused with
/// <see cref="MessageBusOutcome.QueueFlagsDiffer"/> (AMQP 0-9-1 class <c>queue</c>, method
/// <c>declare</c>).
/// </summary>
/// <param name="Durable">Whether the queue and its messages outlive the process when a data
/// directory is given (ADR-0100, decision 3.7). The bus only records it.</param>
/// <param name="Exclusive">Whether only the declaring connection may bind, consume from or
/// delete it; the queue is deleted when that connection goes.</param>
/// <param name="AutoDelete">Whether the queue is deleted when its last consumer ends.</param>
public readonly record struct BusQueueFlags(bool Durable = false, bool Exclusive = false, bool AutoDelete = false);
