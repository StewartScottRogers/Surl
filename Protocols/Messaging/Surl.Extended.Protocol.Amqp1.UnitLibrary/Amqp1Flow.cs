namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>flow</c> (Part 2 section 2.7.4): updates a session's windows and, with a handle, a link's credit.
/// </summary>
/// <param name="IncomingWindow">The sender's incoming window; mandatory.</param>
/// <param name="NextOutgoingId">The transfer-id the sender will give its next transfer; mandatory.</param>
/// <param name="OutgoingWindow">The sender's outgoing window; mandatory.</param>
public sealed record Amqp1Flow(uint IncomingWindow, uint NextOutgoingId, uint OutgoingWindow) : Amqp1Performative
{
    /// <summary>The transfer-id the sender expects next; <see langword="null"/> before it has received a begin.</summary>
    public uint? NextIncomingId { get; init; }

    /// <summary>The link the flow is about; <see langword="null"/> for a session-only flow.</summary>
    public uint? Handle { get; init; }

    /// <summary>The link's delivery count; <see langword="null"/> when absent.</summary>
    public uint? DeliveryCount { get; init; }

    /// <summary>The link's credit; <see langword="null"/> when absent.</summary>
    public uint? LinkCredit { get; init; }

    /// <summary>How many messages the link's sender has waiting; <see langword="null"/> when absent.</summary>
    public uint? Available { get; init; }

    /// <summary><see langword="true"/> when the sender should use up its credit or else give it back.</summary>
    public bool Drain { get; init; }

    /// <summary><see langword="true"/> when the sender asks for a flow in answer.</summary>
    public bool Echo { get; init; }

    /// <summary>Link state properties; <see langword="null"/> when absent.</summary>
    public Amqp1Map? Properties { get; init; }

    /// <summary>The <c>txn-id</c> the <see cref="Properties"/> carry (Part 4 section 4.4.3); <see langword="null"/> when they carry none.</summary>
    public byte[]? TransactionId =>
        Properties?.Entries.FirstOrDefault(entry => entry.Key is Amqp1Symbol { Value: "txn-id" }).Value as byte[];

    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.Flow;

    internal static Amqp1Flow FromFields(Amqp1Fields fields) =>
        new(fields.Mandatory<uint>(1), fields.Mandatory<uint>(2), fields.Mandatory<uint>(3))
        {
            NextIncomingId = fields.OptionalValue<uint>(0),
            Handle = fields.OptionalValue<uint>(4),
            DeliveryCount = fields.OptionalValue<uint>(5),
            LinkCredit = fields.OptionalValue<uint>(6),
            Available = fields.OptionalValue<uint>(7),
            Drain = fields.ValueOr(8, false),
            Echo = fields.ValueOr(9, false),
            Properties = fields.Optional<Amqp1Map>(10),
        };

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() =>
    [
        NextIncomingId,
        IncomingWindow,
        NextOutgoingId,
        OutgoingWindow,
        Handle,
        DeliveryCount,
        LinkCredit,
        Available,
        Amqp1Fields.NullWhenDefault(Drain, false),
        Amqp1Fields.NullWhenDefault(Echo, false),
        Properties,
    ];
}
