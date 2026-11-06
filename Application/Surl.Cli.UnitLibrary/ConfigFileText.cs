using System.Text;

namespace Surl.Cli;

/// <summary>
/// Splits a config file into its option lines by ADR-0101 decision 1's syntax rows, as upstream
/// curl 8.21.0 reads <c>-K</c> files, with the decision's marked departures: UTF-8, lines split at
/// LF with one CR before it dropped, blank and <c>#</c> lines skipped, a name without dashes ending
/// at a space, tab, <c>=</c> or <c>:</c>, a dashed name at a space or tab, a quoted value with its
/// escapes, and text after a value or a missing closing quote refused.
/// </summary>
internal static class ConfigFileText
{
    /// <summary>The longest line, in bytes without its line end, as curl (decision 1 row 16).</summary>
    public const int MaxLineBytes = 10485760;

    /// <summary>The largest config file, in bytes (decision 1 row 17).</summary>
    public const int MaxFileBytes = 16777216;

    /// <summary>A line past <see cref="MaxLineBytes"/> (decision 5 row 3).</summary>
    public const string LineTooLong = "the line is longer than 10485760 bytes";

    /// <summary>A line that is not UTF-8 (decision 5 row 4).</summary>
    public const string NotUtf8 = "not UTF-8";

    private const string LongOptionPrefix = "--";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    /// <summary>Text after a value (decision 5 row 5); <paramref name="name"/> is the option without its value.</summary>
    /// <param name="name">The option as written, without its value.</param>
    /// <returns>The refusal.</returns>
    public static string TextFollowsTheValue(string name) =>
        $"text follows the value of {name}; quote a value that holds a space";

    /// <summary>A quoted value with no closing quote (decision 5 row 6).</summary>
    /// <param name="name">The option as written, without its value.</param>
    /// <returns>The refusal.</returns>
    public static string NoClosingQuote(string name) => $"the value of {name} has no closing double quote";

    /// <summary>
    /// The option lines of <paramref name="content"/>, in file order, skipping blank and comment lines;
    /// a refused line carries its <see cref="ConfigFileLine.Failure"/>, and the reader stops at it.
    /// </summary>
    /// <param name="content">The file's bytes.</param>
    /// <returns>The option lines.</returns>
    public static IEnumerable<ConfigFileLine> Lines(byte[] content)
    {
        var position = content.AsSpan().StartsWith(ByteOrderMark) ? ByteOrderMark.Length : 0;
        var number = 1;
        while (position < content.Length)
        {
            var lineFeed = Array.IndexOf(content, (byte)'\n', position);
            var end = lineFeed < 0 ? content.Length : lineFeed;
            var line = ReadLine(content.AsSpan(position, end - position), number);
            if (line is not null)
            {
                yield return line;
            }

            position = end + 1;
            number++;
        }
    }

    /// <summary>Reads one physical line, without its LF; null for a blank or comment line.</summary>
    private static ConfigFileLine? ReadLine(ReadOnlySpan<byte> bytes, int number)
    {
        if (bytes.EndsWith((byte)'\r'))
        {
            bytes = bytes[..^1];
        }

        if (bytes.Length > MaxLineBytes)
        {
            return Refused(number, LineTooLong);
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Refused(number, NotUtf8);
        }

        return ReadOption(text, number);
    }

    /// <summary>Reads the option and value of a decoded line; null for a blank or comment line.</summary>
    internal static ConfigFileLine? ReadOption(string text, int number)
    {
        var start = SkipBlanks(text, 0);
        if (start == text.Length || text[start] == '#')
        {
            return null;
        }

        var (optionWord, valueStart) = text[start] == '-' ? ReadDashedName(text, start) : ReadUndashedName(text, start);
        if (valueStart == text.Length)
        {
            return new(number, [optionWord], null);
        }

        return text[valueStart] == '"'
            ? ReadQuotedValue(text, valueStart + 1, optionWord, number)
            : ReadUnquotedValue(text, valueStart, optionWord, number);
    }

    private static ConfigFileLine ReadUnquotedValue(string text, int start, string optionWord, int number)
    {
        var end = EndOfWord(text, start);
        return OnlyACommentFollows(text, end)
            ? new(number, [optionWord, text[start..end]], null)
            : Refused(number, TextFollowsTheValue(NameWithoutValue(optionWord)));
    }

    /// <summary>A dashed name (decision 1 row 6): the word as written, its value after spaces and tabs.</summary>
    private static (string OptionWord, int ValueStart) ReadDashedName(string text, int start)
    {
        var end = EndOfWord(text, start);
        return (text[start..end], SkipBlanks(text, end));
    }

    /// <summary>A name without dashes (decision 1 row 5): <c>--</c> and the name, its value after any run of separators.</summary>
    private static (string OptionWord, int ValueStart) ReadUndashedName(string text, int start)
    {
        var end = EndOfUndashedName(text, start);
        return (LongOptionPrefix + text[start..end], SkipSeparators(text, end));
    }

    private static ConfigFileLine ReadQuotedValue(string text, int start, string optionWord, int number)
    {
        var value = new StringBuilder();
        var index = start;
        while (index < text.Length && text[index] != '"')
        {
            index = AppendQuotedCharacter(text, index, value);
        }

        if (index == text.Length)
        {
            return Refused(number, NoClosingQuote(NameWithoutValue(optionWord)));
        }

        return OnlyACommentFollows(text, index + 1)
            ? new(number, [optionWord, value.ToString()], null)
            : Refused(number, TextFollowsTheValue(NameWithoutValue(optionWord)));
    }

    /// <summary>
    /// Appends the quoted character at <paramref name="index"/>, or the one a backslash there and the
    /// character after it stand for, to <paramref name="value"/>; returns the index after them.
    /// </summary>
    private static int AppendQuotedCharacter(string text, int index, StringBuilder value)
    {
        if (text[index] == '\\' && index + 1 < text.Length)
        {
            value.Append(Unescape(text[index + 1]));
            return index + 2;
        }

        value.Append(text[index]);
        return index + 1;
    }

    /// <summary>The character a backslash and <paramref name="escaped"/> stand for inside quotes (decision 1 row 9).</summary>
    private static char Unescape(char escaped) =>
        escaped switch
        {
            't' => '\t',
            'n' => '\n',
            'r' => '\r',
            'v' => '\v',
            _ => escaped,
        };

    /// <summary>
    /// The option word without any value written into it, so a refusal never echoes a value:
    /// <c>--header</c> of <c>--header=X</c>; of a short bundle, up to the first option that takes a
    /// value (<c>-u</c> of <c>-ua:b</c>).
    /// </summary>
    /// <param name="optionWord">The option word as written.</param>
    /// <returns>The name.</returns>
    internal static string NameWithoutValue(string optionWord)
    {
        if (optionWord.StartsWith(LongOptionPrefix, StringComparison.Ordinal))
        {
            var equals = optionWord.IndexOf('=', StringComparison.Ordinal);
            return equals < 0 ? optionWord : optionWord[..equals];
        }

        for (var index = 1; index < optionWord.Length; index++)
        {
            if (CommandLineOptions.TryFindShort(optionWord[index], out var option) && option.Kind != CommandLineOptionKind.Flag)
            {
                return optionWord[..(index + 1)];
            }
        }

        return optionWord;
    }

    private static bool OnlyACommentFollows(string text, int index)
    {
        var next = SkipBlanks(text, index);
        return next == text.Length || text[next] == '#';
    }

    private static int EndOfWord(string text, int start)
    {
        var end = start;
        while (end < text.Length && !IsBlank(text[end]))
        {
            end++;
        }

        return end;
    }

    private static int EndOfUndashedName(string text, int start)
    {
        var end = start;
        while (end < text.Length && !IsSeparator(text[end]))
        {
            end++;
        }

        return end;
    }

    private static int SkipBlanks(string text, int index)
    {
        while (index < text.Length && IsBlank(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int SkipSeparators(string text, int index)
    {
        while (index < text.Length && IsSeparator(text[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsBlank(char character) => character is ' ' or '\t';

    private static bool IsSeparator(char character) => " \t=:".Contains(character, StringComparison.Ordinal);

    private static ConfigFileLine Refused(int number, string failure) => new(number, [], failure);
}
