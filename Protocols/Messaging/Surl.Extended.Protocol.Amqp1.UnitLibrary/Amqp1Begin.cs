namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>begin</c> (Part 2 section 2.7.2): begins a session on a channel.
/// </summary>
/// <param name="NextOutgoingId">The transfer-id of the first transfer the sender will send; mandatory.</param>
/// <param name="IncomingWindow">The sender's initial incoming window; mandatory.</param>
/// <param name="OutgoingWindow">The sender's initial outgoing window; mandatory.</param>
public sealed record Amqp1Begin(uint NextOutgoingId, uint IncomingWindow, uint OutgoingWindow) : Amqp1Performative
{
    /// <summary>The default <see cref="HandleMax"/>: 4294967295.</summary>
    public const uint DefaultHandleMax = uint.MaxValue;

    /// <summary>The channel of the session this begin answers; <see langword="null"/> in a begin that starts one.</summary>
    public ushort? RemoteChannel { get; init; }

    /// <summary>The highest link handle the sender may use.</summary>
    public uint HandleMax { get; init; } = DefaultHandleMax;

    /// <summary>The extension capabilities the sender supports.</summary>
    public IReadOnlyList<Amqp1Symbol> OfferedCapabilities { get; init; } = [];

    /// <summary>The extension capabilities the sender may use if the receiver supports them.</summary>
    public IReadOnlyList<Amqp1Symbol> DesiredCapabilities { get; init; } = [];

    /// <summary>Session properties; <see langword="null"/> when absent.</summary>
    public Amqp1Map? Properties { get; init; }

    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.Begin;

    internal static Amqp1Begin FromFields(Amqp1Fields fields) =>
        new(fields.Mandatory<uint>(1), fields.Mandatory<uint>(2), fields.Mandatory<uint>(3))
        {
            RemoteChannel = fields.OptionalValue<ushort>(0),
            HandleMax = fields.ValueOr(4, DefaultHandleMax),
            OfferedCapabilities = fields.Symbols(5),
            DesiredCapabilities = fields.Symbols(6),
            Properties = fields.Optional<Amqp1Map>(7),
        };

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() =>
    [
        RemoteChannel,
        NextOutgoingId,
        IncomingWindow,
        OutgoingWindow,
        Amqp1Fields.NullWhenDefault(HandleMax, DefaultHandleMax),
        Amqp1Fields.SymbolsField(OfferedCapabilities),
        Amqp1Fields.SymbolsField(DesiredCapabilities),
        Properties,
    ];
}
