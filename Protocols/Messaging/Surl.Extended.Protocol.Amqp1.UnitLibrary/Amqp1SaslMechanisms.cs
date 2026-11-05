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
    /// <remarks>The field is mandatory, so no mechanism is written as an empty array rather than left out (ADR-0107 decision 2).</remarks>
    internal override IReadOnlyList<object?> FieldValues() =>
        [ServerMechanisms.Count == 0 ? Amqp1Array.OfSymbols(ServerMechanisms) : Amqp1Fields.SymbolsField(ServerMechanisms)];
}
