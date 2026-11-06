namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// A <c>LIKE</c> pattern (Jakarta Messaging 3.1 section 3.8.1.2): <c>_</c> stands for any one character,
/// <c>%</c> for any run of characters, and the escape character, when one is given, makes the character
/// after it literal. The whole value must match.
/// </summary>
internal sealed class Amqp1SelectorLikePattern
{
    private const int AnyOne = -1;
    private const int AnyRun = -2;

    private readonly int[] elements;

    private Amqp1SelectorLikePattern(int[] elements) => this.elements = elements;

    /// <summary>Compiles a pattern.</summary>
    /// <param name="pattern">The pattern's text.</param>
    /// <param name="escape">The escape character; <see langword="null"/> when there is none.</param>
    /// <returns>The pattern.</returns>
    /// <exception cref="Amqp1SelectorSyntaxException">The pattern ends with the escape character.</exception>
    public static Amqp1SelectorLikePattern Compile(string pattern, char? escape)
    {
        var elements = new List<int>(pattern.Length);
        for (var index = 0; index < pattern.Length; index++)
        {
            elements.Add(pattern[index] == escape ? EscapedElement(pattern, ++index) : Element(pattern[index]));
        }

        return new([.. elements]);
    }

    /// <summary>Whether the whole value matches the pattern.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public bool Matches(string value)
    {
        int element = 0, character = 0, lastRun = -1, lastRunCharacter = 0;
        while (character < value.Length)
        {
            if (MatchesOne(element, value[character]))
            {
                element++;
                character++;
            }
            else if (element < elements.Length && elements[element] == AnyRun)
            {
                lastRun = element++;
                lastRunCharacter = character;
            }
            else if (lastRun >= 0)
            {
                element = lastRun + 1;
                character = ++lastRunCharacter;
            }
            else
            {
                return false;
            }
        }

        return elements.AsSpan(element).IndexOfAnyExcept(AnyRun) < 0;
    }

    private static int Element(char character) => character switch { '_' => AnyOne, '%' => AnyRun, _ => character };

    private static int EscapedElement(string pattern, int index) =>
        index < pattern.Length ? pattern[index] : throw new Amqp1SelectorSyntaxException();

    private bool MatchesOne(int element, char character) =>
        element < elements.Length && (elements[element] == AnyOne || elements[element] == character);
}
