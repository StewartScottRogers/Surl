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
