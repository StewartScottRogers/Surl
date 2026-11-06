namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>detach</c> (Part 2 section 2.7.7): detaches a link from its session.
/// </summary>
/// <param name="Handle">The link; mandatory.</param>
public sealed record Amqp1Detach(uint Handle) : Amqp1Performative
{
    /// <summary><see langword="true"/> when the link is closed, not only detached.</summary>
    public bool Closed { get; init; }

    /// <summary>Why the link was detached; <see langword="null"/> when it was not for an error.</summary>
    public Amqp1Error? Error { get; init; }

    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.Detach;

    internal static Amqp1Detach FromFields(Amqp1Fields fields) => new(fields.Mandatory<uint>(0))
    {
        Closed = fields.ValueOr(1, false),
        Error = fields.Error(2),
    };

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() =>
        [Handle, Amqp1Fields.NullWhenDefault(Closed, false), Error?.ToDescribed()];
}
