namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The <c>header</c> message section (Part 3 section 3.2.1): how the message is to be delivered.
/// A field left out reads as its default.
/// </summary>
public sealed record Amqp1Header : Amqp1DescribedList
{
    /// <summary>The default <see cref="Priority"/>: 4.</summary>
    public const byte DefaultPriority = 4;

    /// <summary><see langword="true"/> when the message must survive the loss of an intermediary.</summary>
    public bool Durable { get; init; }

    /// <summary>The message's priority, 0 to 255.</summary>
    public byte Priority { get; init; } = DefaultPriority;

    /// <summary>The time to live in milliseconds; <see langword="null"/> when the message does not expire this way.</summary>
    public uint? Ttl { get; init; }

    /// <summary><see langword="true"/> when the message has not been acquired before.</summary>
    public bool FirstAcquirer { get; init; }

    /// <summary>How many earlier deliveries of the message failed.</summary>
    public uint DeliveryCount { get; init; }

    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.Header;

    /// <summary>Reads a <c>header</c> from its described list.</summary>
    /// <param name="described">The described value.</param>
    /// <returns>The header.</returns>
    /// <exception cref="Amqp1DecodeException">It is not a <c>header</c>, or a field is of the wrong type.</exception>
    public static Amqp1Header FromDescribed(Amqp1Described described)
    {
        ArgumentNullException.ThrowIfNull(described);

        var fields = Amqp1Fields.OfDescribedList(described, Amqp1Descriptor.Header);
        return new()
        {
            Durable = fields.ValueOr(0, false),
            Priority = fields.ValueOr(1, DefaultPriority),
            Ttl = fields.OptionalValue<uint>(2),
            FirstAcquirer = fields.ValueOr(3, false),
            DeliveryCount = fields.ValueOr(4, 0u),
        };
    }

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() =>
    [
        Amqp1Fields.NullWhenDefault(Durable, false),
        Amqp1Fields.NullWhenDefault(Priority, DefaultPriority),
        Ttl,
        Amqp1Fields.NullWhenDefault(FirstAcquirer, false),
        Amqp1Fields.NullWhenDefault(DeliveryCount, 0u),
    ];
}
