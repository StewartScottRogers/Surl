namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// One word of a master file entry (RFC 1035 section 5.1): its text as written, escapes
/// included, and whether it was a quoted character-string.
/// </summary>
/// <param name="Text">The text, without the quotes; a backslash and what it escapes kept as written.</param>
/// <param name="IsQuoted">Whether the word was written between double quotes.</param>
internal sealed record DnsMasterFileToken(string Text, bool IsQuoted);

/// <summary>
/// One entry of a master file: a directive or a record, spread over one line or over several
/// inside parentheses.
/// </summary>
/// <param name="LineNumber">The entry's first physical line, from 1.</param>
/// <param name="StartsWithBlank">Whether the entry's first line starts with a space or tab, so its owner is the previous one.</param>
/// <param name="Tokens">The entry's words, in order.</param>
internal sealed record DnsMasterFileEntry(int LineNumber, bool StartsWithBlank, IReadOnlyList<DnsMasterFileToken> Tokens);

/// <summary>
/// Splits master file text into entries (RFC 1035 section 5.1): words separated by spaces and
/// tabs, <c>;</c> comments to the end of the line, <c>(</c> and <c>)</c> continuing an entry over
/// lines, double-quoted character-strings, and a backslash escaping the character after it.
/// Lines end in LF or CRLF. Blank and comment-only lines hold no entry.
/// </summary>
internal sealed class DnsMasterFileTokenizer
{
    private readonly List<DnsMasterFileEntry> entries = [];
    private List<DnsMasterFileToken> tokens = [];
    private int parenthesisDepth;
    private int entryLineNumber;
    private bool entryStartsWithBlank;

    private DnsMasterFileTokenizer()
    {
    }

    /// <summary>Returns the entries of <paramref name="text"/>, in order.</summary>
    /// <exception cref="DnsZoneFileException">
    /// A quote is not closed on its line, a <c>)</c> has no <c>(</c>, or a <c>(</c> is never closed.
    /// </exception>
    public static IReadOnlyList<DnsMasterFileEntry> Entries(string text)
    {
        var tokenizer = new DnsMasterFileTokenizer();
        var lines = text.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            tokenizer.ScanLine(index + 1, lines[index].TrimEnd('\r'));
        }

        if (tokenizer.parenthesisDepth > 0)
        {
            throw new DnsZoneFileException(tokenizer.entryLineNumber, "a '(' without ')'");
        }

        return tokenizer.entries;
    }

    private void ScanLine(int lineNumber, string line)
    {
        if (parenthesisDepth == 0)
        {
            BeginEntry(lineNumber, line);
        }

        var position = 0;

        while (position < line.Length && line[position] != ';')
        {
            position = ScanAt(lineNumber, line, position);
        }

        if (parenthesisDepth == 0 && tokens.Count > 0)
        {
            entries.Add(new DnsMasterFileEntry(entryLineNumber, entryStartsWithBlank, tokens));
        }
    }

    private void BeginEntry(int lineNumber, string line)
    {
        entryLineNumber = lineNumber;
        entryStartsWithBlank = line.Length > 0 && IsBlank(line[0]);
        tokens = [];
    }

    private int ScanAt(int lineNumber, string line, int position)
    {
        var character = line[position];

        if (IsBlank(character))
        {
            return position + 1;
        }

        if (character is '(' or ')')
        {
            ChangeParenthesisDepth(lineNumber, character);
            return position + 1;
        }

        return character == '"' ? ReadQuoted(lineNumber, line, position + 1) : ReadPlain(line, position);
    }

    private void ChangeParenthesisDepth(int lineNumber, char parenthesis)
    {
        if (parenthesis == '(')
        {
            parenthesisDepth++;
        }
        else
        {
            CloseParenthesis(lineNumber);
        }
    }

    private static bool IsBlank(char character) => character is ' ' or '\t';

    private void CloseParenthesis(int lineNumber)
    {
        if (parenthesisDepth == 0)
        {
            throw new DnsZoneFileException(lineNumber, "a ')' without '('");
        }

        parenthesisDepth--;
    }

    private int ReadQuoted(int lineNumber, string line, int start)
    {
        var position = start;

        while (position < line.Length && line[position] != '"')
        {
            position += line[position] == '\\' ? 2 : 1;
        }

        if (position >= line.Length)
        {
            throw new DnsZoneFileException(lineNumber, "an unclosed quote");
        }

        tokens.Add(new DnsMasterFileToken(line[start..position], IsQuoted: true));
        return position + 1;
    }

    private int ReadPlain(string line, int start)
    {
        var position = start;

        while (position < line.Length && !EndsPlainWord(line[position]))
        {
            position += line[position] == '\\' ? 2 : 1;
        }

        position = Math.Min(position, line.Length);
        tokens.Add(new DnsMasterFileToken(line[start..position], IsQuoted: false));
        return position;
    }

    private static bool EndsPlainWord(char character) => " \t;()\"".Contains(character);
}
