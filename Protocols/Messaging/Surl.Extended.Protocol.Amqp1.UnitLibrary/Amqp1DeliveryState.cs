namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The delivery states of Part 3 section 3.4 the container writes and reads: <c>accepted</c> and
/// <c>rejected</c> it sends as a receiver (ADR-0107 decision 5), and every outcome a client
/// receiver may send it.
/// </summary>
public static class Amqp1DeliveryState
{
    private const int UndeliverableHereFieldIndex = 1;

    private static readonly Dictionary<ulong, Amqp1Outcome> OutcomesByDescriptor = new()
    {
        [Amqp1Descriptor.Received] = Amqp1Outcome.Received,
        [Amqp1Descriptor.Accepted] = Amqp1Outcome.Accepted,
        [Amqp1Descriptor.Rejected] = Amqp1Outcome.Rejected,
        [Amqp1Descriptor.Released] = Amqp1Outcome.Released,
        [Amqp1Descriptor.Modified] = Amqp1Outcome.Modified,
    };

    /// <summary><c>accepted</c>: an empty described list.</summary>
    public static Amqp1Described Accepted { get; } = new(Amqp1Descriptor.Accepted, Array.Empty<object?>());

    /// <summary><c>rejected</c> carrying <paramref name="error"/>.</summary>
    /// <param name="error">Why the message was refused.</param>
    /// <returns>The delivery state.</returns>
    public static Amqp1Described Rejected(Amqp1Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new(Amqp1Descriptor.Rejected, new object?[] { error.ToDescribed() });
    }

    /// <summary><c>declared</c> carrying the new transaction's id (Part 4 section 4.5.5).</summary>
    /// <param name="transactionId">The transaction id.</param>
    /// <returns>The delivery state.</returns>
    public static Amqp1Described Declared(byte[] transactionId)
    {
        ArgumentNullException.ThrowIfNull(transactionId);

        return new(Amqp1Descriptor.Declared, new object?[] { transactionId });
    }

    /// <summary><c>transactional-state</c> naming <paramref name="transactionId"/>, with the provisional <paramref name="outcome"/> (Part 4 section 4.5.6).</summary>
    /// <param name="transactionId">The transaction id.</param>
    /// <param name="outcome">The outcome, such as <see cref="Accepted"/> or <see cref="Rejected"/>.</param>
    /// <returns>The delivery state.</returns>
    public static Amqp1Described TransactionalState(byte[] transactionId, Amqp1Described outcome)
    {
        ArgumentNullException.ThrowIfNull(transactionId);
        ArgumentNullException.ThrowIfNull(outcome);

        return new(Amqp1Descriptor.TransactionalState, new object?[] { transactionId, outcome });
    }

    /// <summary><c>transactional-state</c> naming <paramref name="transactionId"/> with no outcome: how a transfer says its delivery is acquired in that transaction (Part 4 section 4.4.3).</summary>
    /// <param name="transactionId">The transaction id.</param>
    /// <returns>The delivery state.</returns>
    public static Amqp1Described TransactionalAcquisition(byte[] transactionId)
    {
        ArgumentNullException.ThrowIfNull(transactionId);

        return new(Amqp1Descriptor.TransactionalState, new object?[] { transactionId });
    }

    /// <summary>The outcome a <c>transactional-state</c> carries.</summary>
    /// <param name="state">A delivery state, as read.</param>
    /// <returns>The outcome as read; <see langword="null"/> when the state is no <c>transactional-state</c> or carries none.</returns>
    public static object? TransactionalOutcomeOf(object? state) =>
        IsTransactional(state) && ((Amqp1Described)state!).Value is IReadOnlyList<object?> fields
            ? fields.ElementAtOrDefault(1)
            : null;

    /// <summary>Whether a delivery state read is a <c>transactional-state</c>.</summary>
    /// <param name="state">The <c>state</c> field of a <c>transfer</c> or <c>disposition</c>, as read.</param>
    /// <returns><see langword="true"/> for a <c>transactional-state</c>, whatever its fields hold.</returns>
    public static bool IsTransactional(object? state) =>
        Amqp1Descriptor.CodeOf((state as Amqp1Described)?.Descriptor) == Amqp1Descriptor.TransactionalState;

    /// <summary>The <c>txn-id</c> a <c>transactional-state</c> names.</summary>
    /// <param name="state">A delivery state, as read.</param>
    /// <returns>The id; <see langword="null"/> when the state is no <c>transactional-state</c> or its <c>txn-id</c> is not a binary.</returns>
    public static byte[]? TransactionIdOf(object? state) =>
        IsTransactional(state) && ((Amqp1Described)state!).Value is IReadOnlyList<object?> fields
            ? fields.ElementAtOrDefault(0) as byte[]
            : null;

    /// <summary>Which outcome a delivery state read is.</summary>
    /// <param name="state">The <c>state</c> field of a <c>disposition</c> or <c>transfer</c>, as read.</param>
    /// <returns>The outcome; <see cref="Amqp1Outcome.None"/> when absent or not a delivery state.</returns>
    public static Amqp1Outcome OutcomeOf(object? state) =>
        Amqp1Descriptor.CodeOf((state as Amqp1Described)?.Descriptor) is { } code && OutcomesByDescriptor.TryGetValue(code, out var outcome)
            ? outcome
            : Amqp1Outcome.None;

    /// <summary>Whether a <c>modified</c> state asks that the message not come back to this link (<c>undeliverable-here</c>).</summary>
    /// <param name="state">A delivery state, as read.</param>
    /// <returns><see langword="true"/> for a <c>modified</c> whose <c>undeliverable-here</c> is true.</returns>
    public static bool IsUndeliverableHere(object? state) =>
        OutcomeOf(state) == Amqp1Outcome.Modified
        && ((Amqp1Described)state!).Value is IReadOnlyList<object?> fields
        && fields.ElementAtOrDefault(UndeliverableHereFieldIndex) is true;
}
