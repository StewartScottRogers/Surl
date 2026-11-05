namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The fields of a described list (Part 1 section 1.4), read by position. A field past the end of
/// the list reads as <see langword="null"/>, so trailing null fields left out are their defaults.
/// A mandatory field that is null, or a field of the wrong type, throws
/// <see cref="Amqp1DecodeException"/> with <see cref="Amqp1ErrorCondition.DecodeError"/>.
/// </summary>
/// <param name="values">The list's elements.</param>
internal sealed class Amqp1Fields(IReadOnlyList<object?> values)
{
    /// <summary>The fields of <paramref name="described"/>, which must be a list with the descriptor <paramref name="descriptorCode"/>.</summary>
    public static Amqp1Fields OfDescribedList(Amqp1Described described, ulong descriptorCode) =>
        Amqp1Descriptor.CodeOf(described.Descriptor) == descriptorCode && described.Value is IReadOnlyList<object?> fields
            ? new(fields)
            : throw new Amqp1DecodeException($"A value is not the described list 0x{descriptorCode:X2}.");

    /// <summary>The field as it was read, of whichever type; <see langword="null"/> when absent.</summary>
    public object? Any(int index) => index < values.Count ? values[index] : null;

    /// <summary>A mandatory field of type <typeparamref name="T"/>.</summary>
    public T Mandatory<T>(int index)
        where T : notnull => Any(index) switch
        {
            T value => value,
            null => throw new Amqp1DecodeException($"Mandatory field {index} is missing."),
            _ => throw WrongType(index),
        };

    /// <summary>An optional field of reference type <typeparamref name="T"/>; <see langword="null"/> when absent.</summary>
    public T? Optional<T>(int index)
        where T : class => Any(index) switch
        {
            null => null,
            T value => value,
            _ => throw WrongType(index),
        };

    /// <summary>An optional field of value type <typeparamref name="T"/>; <see langword="null"/> when absent.</summary>
    public T? OptionalValue<T>(int index)
        where T : struct => Any(index) switch
        {
            null => null,
            T value => value,
            _ => throw WrongType(index),
        };

    /// <summary>A field of value type <typeparamref name="T"/> with a default; the default when absent.</summary>
    public T ValueOr<T>(int index, T defaultValue)
        where T : struct => OptionalValue<T>(index) ?? defaultValue;

    /// <summary>
    /// A field of type <c>symbol</c> with <c>multiple="true"</c>: absent is none, a single symbol is
    /// one, and an array of symbols is each of them (Part 1 section 1.4).
    /// </summary>
    public IReadOnlyList<Amqp1Symbol> Symbols(int index) => Any(index) switch
    {
        null => [],
        Amqp1Symbol symbol => [symbol],
        Amqp1Array array when array.Elements.All(element => element is Amqp1Symbol) => [.. array.Elements.Cast<Amqp1Symbol>()],
        _ => throw WrongType(index),
    };

    /// <summary>A mandatory field of type <c>symbol</c> with <c>multiple="true"</c>.</summary>
    public IReadOnlyList<Amqp1Symbol> MandatorySymbols(int index) =>
        Any(index) is null ? throw new Amqp1DecodeException($"Mandatory field {index} is missing.") : Symbols(index);

    /// <summary>An optional field of type <c>error</c>.</summary>
    public Amqp1Error? Error(int index) =>
        Optional<Amqp1Described>(index) is { } described ? Amqp1Error.FromDescribed(described) : null;

    /// <summary>The value to write for a field with a default: <see langword="null"/> when it is the default.</summary>
    public static object? NullWhenDefault<T>(T value, T defaultValue)
        where T : struct => EqualityComparer<T>.Default.Equals(value, defaultValue) ? null : value;

    /// <summary>The value to write for a field with <c>multiple="true"</c>: null for none, the symbol for one, an array for more.</summary>
    public static object? SymbolsField(IReadOnlyList<Amqp1Symbol> symbols) => symbols.Count switch
    {
        0 => null,
        1 => symbols[0],
        _ => Amqp1Array.OfSymbols(symbols),
    };

    /// <summary>The fields to write, with the trailing null fields left out (Part 1 section 1.4).</summary>
    public static IReadOnlyList<object?> WithoutTrailingNulls(IReadOnlyList<object?> fields)
    {
        var count = fields.Count;
        while (count > 0 && fields[count - 1] is null)
        {
            count--;
        }

        return [.. fields.Take(count)];
    }

    private static Amqp1DecodeException WrongType(int index) => new($"Field {index} is of the wrong type.");
}
