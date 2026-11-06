namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>transfer</c> (Part 2 section 2.7.5): carries a message, or part of one, on a link. The
/// message bytes follow the performative in the frame body.
/// </summary>
/// <param name="Handle">The link; mandatory.</param>
public sealed record Amqp1Transfer(uint Handle) : Amqp1Performative
{
    /// <summary>The delivery-id; <see langword="null"/> on a continuation transfer.</summary>
    public uint? DeliveryId { get; init; }

    /// <summary>The delivery tag; <see langword="null"/> on a continuation transfer.</summary>
    public byte[]? DeliveryTag { get; init; }

    /// <summary>The message format; <see langword="null"/> on a continuation transfer.</summary>
    public uint? MessageFormat { get; init; }

    /// <summary>Whether the delivery is settled; <see langword="null"/> when absent.</summary>
    public bool? Settled { get; init; }

    /// <summary><see langword="true"/> when more transfers of this delivery follow.</summary>
    public bool More { get; init; }

    /// <summary><c>rcv-settle-mode</c> for this delivery; <see langword="null"/> when absent.</summary>
    public byte? ReceiverSettleMode { get; init; }

    /// <summary>The delivery state, as read (a described delivery state, Part 3 section 3.4); <see langword="null"/> when absent.</summary>
    public object? State { get; init; }

    /// <summary><see langword="true"/> when the transfer resumes a delivery.</summary>
    public bool Resume { get; init; }

    /// <summary><see langword="true"/> when the delivery is aborted.</summary>
    public bool Aborted { get; init; }

    /// <summary><see langword="true"/> when the sender does not need an immediate disposition.</summary>
    public bool Batchable { get; init; }

    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.Transfer;

    internal static Amqp1Transfer FromFields(Amqp1Fields fields) => new(fields.Mandatory<uint>(0))
    {
        DeliveryId = fields.OptionalValue<uint>(1),
        DeliveryTag = fields.Optional<byte[]>(2),
        MessageFormat = fields.OptionalValue<uint>(3),
        Settled = fields.OptionalValue<bool>(4),
        More = fields.ValueOr(5, false),
        ReceiverSettleMode = fields.OptionalValue<byte>(6),
        State = fields.Any(7),
        Resume = fields.ValueOr(8, false),
        Aborted = fields.ValueOr(9, false),
        Batchable = fields.ValueOr(10, false),
    };

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() =>
    [
        Handle,
        DeliveryId,
        DeliveryTag,
        MessageFormat,
        Settled,
        Amqp1Fields.NullWhenDefault(More, false),
        ReceiverSettleMode,
        State,
        Amqp1Fields.NullWhenDefault(Resume, false),
        Amqp1Fields.NullWhenDefault(Aborted, false),
        Amqp1Fields.NullWhenDefault(Batchable, false),
    ];
}
