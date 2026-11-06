namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>sasl-outcome</c> (Part 5 section 5.3.3.5): how authentication ended.
/// </summary>
/// <param name="Code">The <c>sasl-code</c>: 0 ok, 1 auth, 2 sys, 3 sys-perm, 4 sys-temp; mandatory.</param>
public sealed record Amqp1SaslOutcome(byte Code) : Amqp1Performative
{
    /// <summary>Data for the client from a successful mechanism; <see langword="null"/> when absent.</summary>
    public byte[]? AdditionalData { get; init; }

    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.SaslOutcome;

    internal static Amqp1SaslOutcome FromFields(Amqp1Fields fields) => new(fields.Mandatory<byte>(0))
    {
        AdditionalData = fields.Optional<byte[]>(1),
    };

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() => [Code, AdditionalData];
}
