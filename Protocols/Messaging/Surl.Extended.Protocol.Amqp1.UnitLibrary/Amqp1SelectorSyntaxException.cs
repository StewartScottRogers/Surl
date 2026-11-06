namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Thrown inside the selector tokenizer and parser when a selector is refused, and caught by
/// <see cref="Amqp1Selector.Parse"/>; it never escapes the library.
/// </summary>
internal sealed class Amqp1SelectorSyntaxException : Exception
{
    /// <summary>Creates the exception for a malformed selector.</summary>
    public Amqp1SelectorSyntaxException()
        : this(Amqp1SelectorRefusal.Malformed)
    {
    }

    /// <summary>Creates the exception for a refusal.</summary>
    /// <param name="refusal">Why the selector is refused.</param>
    public Amqp1SelectorSyntaxException(Amqp1SelectorRefusal refusal)
        : base($"The selector is refused: {refusal}.")
    {
        Refusal = refusal;
    }

    /// <summary>Why the selector is refused.</summary>
    public Amqp1SelectorRefusal Refusal { get; }
}
