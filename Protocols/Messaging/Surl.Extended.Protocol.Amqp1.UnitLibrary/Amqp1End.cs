namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>end</c> (Part 2 section 2.7.8): ends a session.
/// </summary>
/// <param name="Error">Why the session ended; <see langword="null"/> when it was not for an error.</param>
public sealed record Amqp1End(Amqp1Error? Error = null) : Amqp1Performative
{
    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.End;

    internal static Amqp1End FromFields(Amqp1Fields fields) => new(fields.Error(0));

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() => [Error?.ToDescribed()];
}
