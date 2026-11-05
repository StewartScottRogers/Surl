namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>sasl-mechanisms</c> (Part 5 section 5.3.3.1): the SASL mechanisms the server offers.
/// </summary>
/// <param name="ServerMechanisms">The <c>sasl-server-mechanisms</c> field, most preferred first; mandatory.</param>
public sealed record Amqp1SaslMechanisms(IReadOnlyList<Amqp1Symbol> ServerMechanisms) : Amqp1Performative
{
    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.SaslMechanisms;

    internal static Amqp1SaslMechanisms FromFields(Amqp1Fields fields) => new(fields.MandatorySymbols(0));

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() => [Amqp1Fields.SymbolsField(ServerMechanisms)];
}
