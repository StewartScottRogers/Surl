using System.Collections.Frozen;
using System.Globalization;
using System.Text;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Splits a JMS selector into tokens (Jakarta Messaging 3.1 section 3.8.1.1): string literals with
/// <c>''</c> for a quote, exact numeric literals in Java's integer syntax (decimal, <c>0x</c> hexadecimal,
/// leading-zero octal, an optional <c>L</c>), approximate numeric literals in Java's floating-point
/// syntax (a decimal point or an exponent, or an <c>F</c> or <c>D</c> suffix), keywords in any case,
/// identifiers, and the operators. Anything else throws <see cref="Amqp1SelectorSyntaxException"/>.
/// </summary>
internal sealed class Amqp1SelectorTokenizer
{
    private static readonly FrozenDictionary<string, Amqp1SelectorToken> Keywords =
        new Dictionary<string, Amqp1SelectorToken>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOT"] = new(Amqp1SelectorTokenKind.Not),
            ["AND"] = new(Amqp1SelectorTokenKind.And),
            ["OR"] = new(Amqp1SelectorTokenKind.Or),
            ["BETWEEN"] = new(Amqp1SelectorTokenKind.Between),
            ["LIKE"] = new(Amqp1SelectorTokenKind.Like),
            ["IN"] = new(Amqp1SelectorTokenKind.In),
            ["IS"] = new(Amqp1SelectorTokenKind.Is),
            ["NULL"] = new(Amqp1SelectorTokenKind.Null),
            ["ESCAPE"] = new(Amqp1SelectorTokenKind.Escape),
            ["TRUE"] = new(Amqp1SelectorTokenKind.Literal, true),
            ["FALSE"] = new(Amqp1SelectorTokenKind.Literal, false),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<char, Amqp1SelectorTokenKind> Punctuation =
        new Dictionary<char, Amqp1SelectorTokenKind>
        {
            ['('] = Amqp1SelectorTokenKind.LeftParenthesis,
            [')'] = Amqp1SelectorTokenKind.RightParenthesis,
            [','] = Amqp1SelectorTokenKind.Comma,
            ['='] = Amqp1SelectorTokenKind.Equal,
            ['+'] = Amqp1SelectorTokenKind.Plus,
            ['-'] = Amqp1SelectorTokenKind.Minus,
            ['*'] = Amqp1SelectorTokenKind.Multiply,
            ['/'] = Amqp1SelectorTokenKind.Divide,
        }.ToFrozenDictionary();

    private static readonly Func<char, bool> WhiteSpace = char.IsWhiteSpace;
    private static readonly Func<char, bool> DecimalDigit = IsDigit;
    private static readonly Func<char, bool> HexadecimalDigit = char.IsAsciiHexDigit;
    private static readonly Func<char, bool> IdentifierPart = IsIdentifierPart;

    private readonly string text;
    private int position;

    private Amqp1SelectorTokenizer(string text) => this.text = text;

    /// <summary>Splits a selector into its tokens, ending with an <see cref="Amqp1SelectorTokenKind.End"/> token.</summary>
    /// <param name="text">The selector.</param>
    /// <returns>The tokens.</returns>
    /// <exception cref="Amqp1SelectorSyntaxException">The selector holds something that is not a token.</exception>
    public static List<Amqp1SelectorToken> Tokenize(string text)
    {
        var tokenizer = new Amqp1SelectorTokenizer(text);
        var tokens = new List<Amqp1SelectorToken>();
        Amqp1SelectorToken token;
        do
        {
            token = tokenizer.ReadToken();
            tokens.Add(token);
        }
        while (token.Kind != Amqp1SelectorTokenKind.End);

        return tokens;
    }

    private static bool IsIdentifierStart(char character) => char.IsLetter(character) || character is '_' or '$';

    private static bool IsIdentifierPart(char character) => IsIdentifierStart(character) || char.IsDigit(character);

    private static bool IsDigit(char character) => character is >= '0' and <= '9';

    private static long ParseInteger(string digits)
    {
        if (digits.Length > 1 && digits[0] == '0')
        {
            return ParseOctal(digits);
        }

        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new Amqp1SelectorSyntaxException();
    }

    private static long ParseOctal(string digits)
    {
        long value = 0;
        foreach (var digit in digits)
        {
            if (digit > '7' || value > long.MaxValue >> 3)
            {
                throw new Amqp1SelectorSyntaxException();
            }

            value = (value << 3) | (long)(digit - '0');
        }

        return value;
    }

    private char Peek(int offset = 0) => position + offset < text.Length ? text[position + offset] : '\0';

    private Amqp1SelectorToken ReadToken()
    {
        ReadWhile(WhiteSpace);
        if (position == text.Length)
        {
            return new(Amqp1SelectorTokenKind.End);
        }

        var character = text[position];
        if (character == '\'')
        {
            return ReadString();
        }

        if (StartsNumber(character))
        {
            return ReadNumber();
        }

        return IsIdentifierStart(character) ? ReadWord() : ReadOperator(character);
    }

    private bool StartsNumber(char character) => IsDigit(character) || (character == '.' && IsDigit(Peek(1)));

    private bool StartsHexadecimal() => Peek() == '0' && Peek(1) is 'x' or 'X';

    private bool EndsNumber() => !IsIdentifierPart(Peek()) && Peek() != '.';

    private Amqp1SelectorToken ReadString()
    {
        var value = new StringBuilder();
        position++;
        while (position < text.Length)
        {
            var character = text[position++];
            if (character == '\'' && !Accept('\''))
            {
                return new(Amqp1SelectorTokenKind.Literal, value.ToString());
            }

            value.Append(character);
        }

        throw new Amqp1SelectorSyntaxException();
    }

    private Amqp1SelectorToken ReadWord()
    {
        var word = ReadWhile(IdentifierPart);
        return Keywords.TryGetValue(word, out var keyword) ? keyword : new(Amqp1SelectorTokenKind.Identifier, word);
    }

    private Amqp1SelectorToken ReadOperator(char character)
    {
        position++;
        if (character == '<')
        {
            return ReadAfterLess();
        }

        if (character == '>')
        {
            return Accept('=') ? new(Amqp1SelectorTokenKind.GreaterOrEqual) : new(Amqp1SelectorTokenKind.Greater);
        }

        return Punctuation.TryGetValue(character, out var kind) ? new(kind) : throw new Amqp1SelectorSyntaxException();
    }

    private Amqp1SelectorToken ReadAfterLess()
    {
        if (Accept('='))
        {
            return new(Amqp1SelectorTokenKind.LessOrEqual);
        }

        return Accept('>') ? new(Amqp1SelectorTokenKind.NotEqual) : new(Amqp1SelectorTokenKind.Less);
    }

    private Amqp1SelectorToken ReadNumber()
    {
        var value = StartsHexadecimal() ? ReadHexadecimal() : ReadDecimal();
        return EndsNumber() ? new(Amqp1SelectorTokenKind.Literal, value) : throw new Amqp1SelectorSyntaxException();
    }

    private object ReadHexadecimal()
    {
        position += 2;
        var digits = ReadWhile(HexadecimalDigit);
        Accept('L', 'l');
        return digits.Length is > 0 and <= 16
            ? long.Parse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)
            : throw new Amqp1SelectorSyntaxException();
    }

    private object ReadDecimal()
    {
        var start = position;
        var integerPart = ReadWhile(DecimalDigit);
        var isApproximate = ReadFraction() | ReadExponent();
        var body = text[start..position];
        if (Accept('F', 'f', 'D', 'd') || isApproximate)
        {
            return double.Parse(body, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        Accept('L', 'l');
        return ParseInteger(integerPart);
    }

    private bool ReadFraction()
    {
        if (!Accept('.'))
        {
            return false;
        }

        ReadWhile(DecimalDigit);
        return true;
    }

    private bool ReadExponent()
    {
        if (!Accept('E', 'e'))
        {
            return false;
        }

        Accept('+', '-');
        return ReadWhile(DecimalDigit).Length > 0 ? true : throw new Amqp1SelectorSyntaxException();
    }

    private string ReadWhile(Func<char, bool> belongs)
    {
        var start = position;
        while (position < text.Length && belongs(text[position]))
        {
            position++;
        }

        return text[start..position];
    }

    private bool Accept(params ReadOnlySpan<char> expected)
    {
        if (position == text.Length || !expected.Contains(text[position]))
        {
            return false;
        }

        position++;
        return true;
    }
}
