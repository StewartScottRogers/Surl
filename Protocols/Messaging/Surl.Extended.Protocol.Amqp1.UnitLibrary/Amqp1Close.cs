namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>close</c> (Part 2 section 2.7.9): closes the connection.
/// </summary>
/// <param name="Error">Why the connection closed; <see langword="null"/> when it was not for an error.</param>
public sealed record Amqp1Close(Amqp1Error? Error = null) : Amqp1Performative
{
    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.Close;

    internal static Amqp1Close FromFields(Amqp1Fields fields) => new(fields.Error(0));

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() => [Error?.ToDescribed()];
}
