using System.Text;

namespace Surl.Protocol.Dict;

/// <summary>
/// Splits a DICT command line into its words (RFC 2229, section 2.2).
/// </summary>
/// <remarks>
/// Words are separated by spaces or tabs. A word may be quoted, in whole or in part, with
/// <c>"</c> or <c>'</c>, and inside or outside quotes a backslash takes the next character
/// literally. Upstream curl 8.21.0 quotes nothing and escapes a space, a quote or a
/// backslash in a word with a backslash, so <c>dict://host/d:a%20b</c> arrives as
/// <c>DEFINE ! a\ b</c> and is split into <c>DEFINE</c>, <c>!</c> and <c>a b</c>.
/// </remarks>
internal sealed class DictCommandTokenizer
{
    private const string Quotes = "\"'";
    private const string WordSeparators = " \t";

    private readonly List<string> words = [];
    private readonly StringBuilder word = new();
    private bool inWord;
    private char openQuote;
    private bool escapeNext;

    private DictCommandTokenizer()
    {
    }

    /// <summary>
    /// Splits <paramref name="line"/> into words.
    /// </summary>
    /// <param name="line">The command line, without its line ending.</param>
    /// <returns>
    /// The words, empty for a blank line; <see langword="null"/> when a quote is not closed
    /// or the line ends in a lone backslash.
    /// </returns>
    public static IReadOnlyList<string>? Split(string line)
    {
        var tokenizer = new DictCommandTokenizer();
        foreach (var character in line)
        {
            tokenizer.Accept(character);
        }

        return tokenizer.Finish();
    }

    private void Accept(char character)
    {
        if (escapeNext)
        {
            escapeNext = false;
            Append(character);
        }
        else if (character == '\\')
        {
            escapeNext = true;
            inWord = true;
        }
        else if (openQuote != '\0')
        {
            AcceptQuoted(character);
        }
        else
        {
            AcceptUnquoted(character);
        }
    }

    private void AcceptQuoted(char character)
    {
        if (character == openQuote)
        {
            openQuote = '\0';
        }
        else
        {
            Append(character);
        }
    }

    private void AcceptUnquoted(char character)
    {
        if (Quotes.Contains(character))
        {
            openQuote = character;
            inWord = true;
        }
        else if (WordSeparators.Contains(character))
        {
            EndWord();
        }
        else
        {
            Append(character);
        }
    }

    private void Append(char character)
    {
        word.Append(character);
        inWord = true;
    }

    private void EndWord()
    {
        if (inWord)
        {
            words.Add(word.ToString());
            word.Clear();
            inWord = false;
        }
    }

    private IReadOnlyList<string>? Finish()
    {
        if (escapeNext || openQuote != '\0')
        {
            return null;
        }

        EndWord();

        return words;
    }
}
