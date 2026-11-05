namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// An AMQP 1.0 <c>array</c> (Part 1 sections 1.2 and 1.6.25): elements that share one constructor,
/// which is kept so that an array read is written back the same.
/// </summary>
/// <param name="ElementFormatCode">The format code every element is written with, from <see cref="Amqp1FormatCode"/>.</param>
/// <param name="Elements">The elements, in order, as <see cref="Amqp1Decoder"/> reads that format code.</param>
/// <param name="ElementDescriptor">
/// The descriptor every element carries when the array's constructor is a described one;
/// <see langword="null"/> when it is not.
/// </param>
public sealed record Amqp1Array(byte ElementFormatCode, IReadOnlyList<object?> Elements, object? ElementDescriptor = null)
{
    /// <summary>
    /// An array of symbols, as a field of type <c>symbol</c> with <c>multiple="true"</c> carries
    /// two or more: <c>sym8</c> elements when every symbol fits in 255 octets, <c>sym32</c> otherwise.
    /// </summary>
    /// <param name="symbols">The symbols.</param>
    /// <returns>The array.</returns>
    public static Amqp1Array OfSymbols(IReadOnlyList<Amqp1Symbol> symbols)
    {
        var fitsInOneOctet = symbols.All(symbol => symbol.Value.Length <= byte.MaxValue);
        return new(fitsInOneOctet ? Amqp1FormatCode.Symbol8 : Amqp1FormatCode.Symbol32, [.. symbols.Cast<object?>()]);
    }
}
