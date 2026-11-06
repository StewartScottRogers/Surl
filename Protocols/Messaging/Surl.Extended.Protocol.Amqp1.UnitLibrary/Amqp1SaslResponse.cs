namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>sasl-response</c> (Part 5 section 5.3.3.4): the client's answer to a challenge.
/// </summary>
/// <param name="Response">The response bytes; mandatory.</param>
public sealed record Amqp1SaslResponse(byte[] Response) : Amqp1Performative
{
    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.SaslResponse;

    internal static Amqp1SaslResponse FromFields(Amqp1Fields fields) => new(fields.Mandatory<byte[]>(0));

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() => [Response];
}
