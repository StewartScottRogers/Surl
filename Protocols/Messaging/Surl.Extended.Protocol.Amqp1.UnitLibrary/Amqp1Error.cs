namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// <c>error</c> (Part 2 section 2.8.14): why a link, session or connection ended.
/// </summary>
/// <param name="Condition">The error condition, one of <see cref="Amqp1ErrorCondition"/>'s or another symbol.</param>
public sealed record Amqp1Error(Amqp1Symbol Condition) : Amqp1DescribedList
{
    /// <summary>A description of the error for a person to read; <see langword="null"/> when absent.</summary>
    public string? Description { get; init; }

    /// <summary>More about the error, keyed by symbol; <see langword="null"/> when absent.</summary>
    public Amqp1Map? Info { get; init; }

    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.Error;

    /// <summary>Reads an <c>error</c> from its described list.</summary>
    /// <param name="described">The described value.</param>
    /// <returns>The error.</returns>
    /// <exception cref="Amqp1DecodeException">It is not an <c>error</c>, or a field is missing or of the wrong type.</exception>
    public static Amqp1Error FromDescribed(Amqp1Described described)
    {
        ArgumentNullException.ThrowIfNull(described);

        var fields = Amqp1Fields.OfDescribedList(described, Amqp1Descriptor.Error);
        return new(fields.Mandatory<Amqp1Symbol>(0))
        {
            Description = fields.Optional<string>(1),
            Info = fields.Optional<Amqp1Map>(2),
        };
    }

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() => [Condition, Description, Info];
}
