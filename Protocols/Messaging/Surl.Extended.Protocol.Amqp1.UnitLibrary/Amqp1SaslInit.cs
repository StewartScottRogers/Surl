namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>sasl-init</c> (Part 5 section 5.3.3.2): the mechanism the client chose, and its first response.
/// </summary>
/// <param name="Mechanism">The chosen mechanism; mandatory.</param>
public sealed record Amqp1SaslInit(Amqp1Symbol Mechanism) : Amqp1Performative
{
    /// <summary>The mechanism's initial response; <see langword="null"/> when absent.</summary>
    public byte[]? InitialResponse { get; init; }

    /// <summary>The name of the host the client is connecting to; <see langword="null"/> when absent.</summary>
    public string? Hostname { get; init; }

    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.SaslInit;

    internal static Amqp1SaslInit FromFields(Amqp1Fields fields) => new(fields.Mandatory<Amqp1Symbol>(0))
    {
        InitialResponse = fields.Optional<byte[]>(1),
        Hostname = fields.Optional<string>(2),
    };

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() => [Mechanism, InitialResponse, Hostname];
}
