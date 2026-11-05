namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>attach</c> (Part 2 section 2.7.3): attaches a link to a session.
/// </summary>
/// <param name="Name">The name of the link; mandatory.</param>
/// <param name="Handle">The handle the sender uses for the link; mandatory.</param>
/// <param name="IsReceiver">The <c>role</c> field: <see langword="false"/> when the sender of this attach is the link's sender, <see langword="true"/> when it is its receiver; mandatory.</param>
public sealed record Amqp1Attach(string Name, uint Handle, bool IsReceiver) : Amqp1Performative
{
    /// <summary>The default <see cref="SenderSettleMode"/>: 2, <c>mixed</c>.</summary>
    public const byte DefaultSenderSettleMode = 2;

    /// <summary>The default <see cref="ReceiverSettleMode"/>: 0, <c>first</c>.</summary>
    public const byte DefaultReceiverSettleMode = 0;

    /// <summary><c>snd-settle-mode</c>: 0 <c>unsettled</c>, 1 <c>settled</c>, 2 <c>mixed</c>.</summary>
    public byte SenderSettleMode { get; init; } = DefaultSenderSettleMode;

    /// <summary><c>rcv-settle-mode</c>: 0 <c>first</c>, 1 <c>second</c>.</summary>
    public byte ReceiverSettleMode { get; init; } = DefaultReceiverSettleMode;

    /// <summary>The source, as read (a described <c>source</c>, Part 3 section 3.5.3); <see langword="null"/> when absent.</summary>
    public object? Source { get; init; }

    /// <summary>The target, as read (a described <c>target</c>, Part 3 section 3.5.4); <see langword="null"/> when absent.</summary>
    public object? Target { get; init; }

    /// <summary>The unsettled delivery state, keyed by delivery tag; <see langword="null"/> when absent.</summary>
    public Amqp1Map? Unsettled { get; init; }

    /// <summary><see langword="true"/> when <see cref="Unsettled"/> is incomplete.</summary>
    public bool IncompleteUnsettled { get; init; }

    /// <summary>The sender's initial delivery count; <see langword="null"/> when absent.</summary>
    public uint? InitialDeliveryCount { get; init; }

    /// <summary>The largest message the sender of this attach accepts; <see langword="null"/> when it sets no limit.</summary>
    public ulong? MaxMessageSize { get; init; }

    /// <summary>The extension capabilities the sender supports.</summary>
    public IReadOnlyList<Amqp1Symbol> OfferedCapabilities { get; init; } = [];

    /// <summary>The extension capabilities the sender may use if the receiver supports them.</summary>
    public IReadOnlyList<Amqp1Symbol> DesiredCapabilities { get; init; } = [];

    /// <summary>Link properties; <see langword="null"/> when absent.</summary>
    public Amqp1Map? Properties { get; init; }

    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.Attach;

    internal static Amqp1Attach FromFields(Amqp1Fields fields) =>
        new(fields.Mandatory<string>(0), fields.Mandatory<uint>(1), fields.Mandatory<bool>(2))
        {
            SenderSettleMode = fields.ValueOr(3, DefaultSenderSettleMode),
            ReceiverSettleMode = fields.ValueOr(4, DefaultReceiverSettleMode),
            Source = fields.Any(5),
            Target = fields.Any(6),
            Unsettled = fields.Optional<Amqp1Map>(7),
            IncompleteUnsettled = fields.ValueOr(8, false),
            InitialDeliveryCount = fields.OptionalValue<uint>(9),
            MaxMessageSize = fields.OptionalValue<ulong>(10),
            OfferedCapabilities = fields.Symbols(11),
            DesiredCapabilities = fields.Symbols(12),
            Properties = fields.Optional<Amqp1Map>(13),
        };

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() =>
    [
        Name,
        Handle,
        IsReceiver,
        Amqp1Fields.NullWhenDefault(SenderSettleMode, DefaultSenderSettleMode),
        Amqp1Fields.NullWhenDefault(ReceiverSettleMode, DefaultReceiverSettleMode),
        Source,
        Target,
        Unsettled,
        Amqp1Fields.NullWhenDefault(IncompleteUnsettled, false),
        InitialDeliveryCount,
        MaxMessageSize,
        Amqp1Fields.SymbolsField(OfferedCapabilities),
        Amqp1Fields.SymbolsField(DesiredCapabilities),
        Properties,
    ];
}
