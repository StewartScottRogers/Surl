namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// A field table (section 4.2.5.5): name-value pairs, kept in the order sent. Names are not
/// required to be unique, as no client relies on it; <see cref="Find"/> returns the first.
/// </summary>
/// <param name="Fields">The fields, in order.</param>
public sealed record Amqp0FieldTable(IReadOnlyList<Amqp0Field> Fields)
{
    /// <summary>The empty table.</summary>
    public static Amqp0FieldTable Empty { get; } = new([]);

    /// <summary>The value of the first field named <paramref name="name"/>, matched exactly.</summary>
    /// <param name="name">The field's name.</param>
    /// <returns>The value, or <see langword="null"/> when no field has that name.</returns>
    public Amqp0FieldValue? Find(string name) =>
        Fields.FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.Ordinal))?.Value;
}
