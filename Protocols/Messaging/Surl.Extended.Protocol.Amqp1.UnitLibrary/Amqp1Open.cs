namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>open</c> (Part 2 section 2.7.1): negotiates a connection's parameters. A field left out
/// reads as its default.
/// </summary>
/// <param name="ContainerId">The id of the sending container; mandatory.</param>
public sealed record Amqp1Open(string ContainerId) : Amqp1Performative
{
    /// <summary>The default <see cref="MaxFrameSize"/>: 4294967295, no limit.</summary>
    public const uint DefaultMaxFrameSize = uint.MaxValue;

    /// <summary>The default <see cref="ChannelMax"/>: 65535.</summary>
    public const ushort DefaultChannelMax = ushort.MaxValue;

    /// <summary>The name of the target host; <see langword="null"/> when absent.</summary>
    public string? Hostname { get; init; }

    /// <summary>The largest frame the sender accepts, header included.</summary>
    public uint MaxFrameSize { get; init; } = DefaultMaxFrameSize;

    /// <summary>The highest channel number the sender may use.</summary>
    public ushort ChannelMax { get; init; } = DefaultChannelMax;

    /// <summary>The sender's idle time-out in milliseconds; <see langword="null"/> when it has none.</summary>
    public uint? IdleTimeOut { get; init; }

    /// <summary>The locales the sender may write in.</summary>
    public IReadOnlyList<Amqp1Symbol> OutgoingLocales { get; init; } = [];

    /// <summary>The locales the sender would read, most preferred first.</summary>
    public IReadOnlyList<Amqp1Symbol> IncomingLocales { get; init; } = [];

    /// <summary>The extension capabilities the sender supports.</summary>
    public IReadOnlyList<Amqp1Symbol> OfferedCapabilities { get; init; } = [];

    /// <summary>The extension capabilities the sender may use if the receiver supports them.</summary>
    public IReadOnlyList<Amqp1Symbol> DesiredCapabilities { get; init; } = [];

    /// <summary>Connection properties; <see langword="null"/> when absent.</summary>
    public Amqp1Map? Properties { get; init; }

    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.Open;

    internal static Amqp1Open FromFields(Amqp1Fields fields) => new(fields.Mandatory<string>(0))
    {
        Hostname = fields.Optional<string>(1),
        MaxFrameSize = fields.ValueOr(2, DefaultMaxFrameSize),
        ChannelMax = fields.ValueOr(3, DefaultChannelMax),
        IdleTimeOut = fields.OptionalValue<uint>(4),
        OutgoingLocales = fields.Symbols(5),
        IncomingLocales = fields.Symbols(6),
        OfferedCapabilities = fields.Symbols(7),
        DesiredCapabilities = fields.Symbols(8),
        Properties = fields.Optional<Amqp1Map>(9),
    };

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() =>
    [
        ContainerId,
        Hostname,
        Amqp1Fields.NullWhenDefault(MaxFrameSize, DefaultMaxFrameSize),
        Amqp1Fields.NullWhenDefault(ChannelMax, DefaultChannelMax),
        IdleTimeOut,
        Amqp1Fields.SymbolsField(OutgoingLocales),
        Amqp1Fields.SymbolsField(IncomingLocales),
        Amqp1Fields.SymbolsField(OfferedCapabilities),
        Amqp1Fields.SymbolsField(DesiredCapabilities),
        Properties,
    ];
}
