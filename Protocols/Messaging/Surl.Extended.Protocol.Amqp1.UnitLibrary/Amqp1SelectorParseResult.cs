namespace Surl.Extended.Protocol.Amqp1;

/// <summary>What <see cref="Amqp1Selector.Parse"/> made of a selector: the selector, or why it was refused.</summary>
/// <param name="Selector">The parsed selector; <see langword="null"/> when it was refused.</param>
/// <param name="Refusal">Why it was refused; <see cref="Amqp1SelectorRefusal.None"/> when it parsed.</param>
public sealed record Amqp1SelectorParseResult(Amqp1Selector? Selector, Amqp1SelectorRefusal Refusal)
{
    /// <summary><see langword="true"/> when the selector parsed.</summary>
    public bool IsParsed => Refusal == Amqp1SelectorRefusal.None;
}
