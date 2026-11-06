namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>disposition</c> (Part 2 section 2.7.6): the state or settlement of a range of deliveries.
/// </summary>
/// <param name="IsReceiver">The <c>role</c> field: <see langword="true"/> when the sender of this disposition is the deliveries' receiver; mandatory.</param>
/// <param name="First">The first delivery-id of the range; mandatory.</param>
public sealed record Amqp1Disposition(bool IsReceiver, uint First) : Amqp1Performative
{
    /// <summary>The last delivery-id of the range; <see langword="null"/> when the range is <see cref="First"/> alone.</summary>
    public uint? Last { get; init; }

    /// <summary><see langword="true"/> when the deliveries are settled.</summary>
    public bool Settled { get; init; }

    /// <summary>The delivery state, as read (a described delivery state, Part 3 section 3.4); <see langword="null"/> when absent.</summary>
    public object? State { get; init; }

    /// <summary><see langword="true"/> when the sender does not need an immediate answer.</summary>
    public bool Batchable { get; init; }

    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.Disposition;

    internal static Amqp1Disposition FromFields(Amqp1Fields fields) =>
        new(fields.Mandatory<bool>(0), fields.Mandatory<uint>(1))
        {
            Last = fields.OptionalValue<uint>(2),
            Settled = fields.ValueOr(3, false),
            State = fields.Any(4),
            Batchable = fields.ValueOr(5, false),
        };

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() =>
    [
        IsReceiver,
        First,
        Last,
        Amqp1Fields.NullWhenDefault(Settled, false),
        State,
        Amqp1Fields.NullWhenDefault(Batchable, false),
    ];
}
