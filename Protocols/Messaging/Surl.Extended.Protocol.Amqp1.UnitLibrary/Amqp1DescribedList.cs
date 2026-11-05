namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// A composite type of AMQP 1.0 (Part 1 section 1.4): a list of fields under a descriptor. The
/// performatives, <c>error</c>, and the <c>header</c> and <c>properties</c> message sections are
/// each one.
/// </summary>
public abstract record Amqp1DescribedList
{
    /// <summary>The numeric descriptor, from <see cref="Amqp1Descriptor"/>.</summary>
    public abstract ulong DescriptorCode { get; }

    /// <summary>
    /// The described list to write: the numeric descriptor and the fields in order, a field at its
    /// default written as null and trailing null fields left out.
    /// </summary>
    /// <returns>The described value.</returns>
    public Amqp1Described ToDescribed() => new(DescriptorCode, Amqp1Fields.WithoutTrailingNulls(FieldValues()));

    /// <summary>Every field in order, a field at its default as null.</summary>
    /// <returns>The field values.</returns>
    internal abstract IReadOnlyList<object?> FieldValues();
}
