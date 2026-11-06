namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>sasl-challenge</c> (Part 5 section 5.3.3.3): a challenge from the server's mechanism.
/// </summary>
/// <param name="Challenge">The challenge bytes; mandatory.</param>
public sealed record Amqp1SaslChallenge(byte[] Challenge) : Amqp1Performative
{
    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.SaslChallenge;

    internal static Amqp1SaslChallenge FromFields(Amqp1Fields fields) => new(fields.Mandatory<byte[]>(0));

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() => [Challenge];
}
