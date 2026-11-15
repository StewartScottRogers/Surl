namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// The state one <see cref="KafkaBroker"/> holds beside the message bus: its idempotent producers' sequence
/// windows and its consumer groups (ADR-0159 decision 2). Reset, snapshot and restore act on both together.
/// </summary>
public sealed class KafkaBrokerState
{
    /// <summary>Creates the state of one broker over the objects its answerers share.</summary>
    /// <param name="producerStates">The producer states Produce and InitProducerId share.</param>
    /// <param name="coordinator">The coordinator every group answerer shares.</param>
    public KafkaBrokerState(KafkaProducerStates producerStates, KafkaGroupCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(producerStates);
        ArgumentNullException.ThrowIfNull(coordinator);
        ProducerStates = producerStates;
        Coordinator = coordinator;
    }

    internal KafkaProducerStates ProducerStates { get; }

    internal KafkaGroupCoordinator Coordinator { get; }

    /// <summary>Empties the producer states and drops every consumer group.</summary>
    public void Reset()
    {
        ProducerStates.Reset();
        Coordinator.Reset();
    }

    /// <summary>Copies the producer states; consumer groups are live state and are not copied.</summary>
    /// <returns>A snapshot only this state restores.</returns>
    public KafkaBrokerStateSnapshot TakeSnapshot() => new(this, ProducerStates.TakeSnapshot());

    /// <summary>Replaces the producer states with the snapshot's and drops every consumer group.</summary>
    /// <param name="snapshot">A snapshot this state took.</param>
    /// <exception cref="InvalidOperationException">The snapshot is another broker's.</exception>
    public void Restore(KafkaBrokerStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!ReferenceEquals(snapshot.Owner, this))
        {
            throw new InvalidOperationException("The snapshot was taken from another Kafka broker.");
        }

        ProducerStates.Restore(snapshot.ProducerStates);
        Coordinator.Reset();
    }
}
