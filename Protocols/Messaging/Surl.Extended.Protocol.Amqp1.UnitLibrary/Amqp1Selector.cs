namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// A JMS message selector (Jakarta Messaging 3.1 section 3.8.1; ADR-0146 decision 2.2), parsed once
/// and then evaluated against any number of messages with SQL's three-valued logic. A message is
/// selected only when the selector is TRUE for it; an empty selector selects every message.
/// </summary>
public sealed class Amqp1Selector
{
    /// <summary>The longest selector accepted, in characters.</summary>
    public const int MaxLength = 4096;

    /// <summary>
    /// The deepest nesting accepted: each parenthesised group, <c>NOT</c> and unary sign opens one level.
    /// </summary>
    public const int MaxDepth = 32;

    private readonly Amqp1SelectorNode? root;

    private Amqp1Selector(string text, Amqp1SelectorNode? root)
    {
        Text = text;
        this.root = root;
    }

    /// <summary>The selector as given.</summary>
    public string Text { get; }

    /// <summary><see langword="true"/> when the selector is empty (or only white space) and selects every message.</summary>
    public bool SelectsEverything => root is null;

    /// <summary>
    /// Parses a selector. Nothing escapes as an exception: a selector longer than
    /// <see cref="MaxLength"/>, nested deeper than <see cref="MaxDepth"/> or that does not parse is
    /// refused with its reason.
    /// </summary>
    /// <param name="text">The selector; <see langword="null"/> or white space selects every message.</param>
    /// <returns>The selector, or the reason it was refused.</returns>
    public static Amqp1SelectorParseResult Parse(string? text)
    {
        var selectorText = text ?? string.Empty;
        if (selectorText.Length > MaxLength)
        {
            return new(null, Amqp1SelectorRefusal.TooLong);
        }

        try
        {
            var root = Amqp1SelectorParser.Parse(selectorText);
            return new(new Amqp1Selector(selectorText, root), Amqp1SelectorRefusal.None);
        }
        catch (Amqp1SelectorSyntaxException exception)
        {
            return new(null, exception.Refusal);
        }
    }

    /// <summary>The selector's value for a message: TRUE, FALSE, or NULL (<see langword="null"/>) when unknown.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The three-valued result; a selector whose value is not a boolean is NULL.</returns>
    public bool? Evaluate(IAmqp1SelectorMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return root is null ? true : Amqp1SelectorValues.AsLogic(root.Evaluate(message));
    }

    /// <summary><see langword="true"/> when the selector is TRUE for the message; FALSE and NULL do not select.</summary>
    /// <param name="message">The message.</param>
    /// <returns>Whether the message is selected.</returns>
    public bool Selects(IAmqp1SelectorMessage message) => Evaluate(message) == true;
}
