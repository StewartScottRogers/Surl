using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Splits a test case's command line into curl's arguments as the POSIX shell upstream's
/// runtests hands it to splits it (<c>tests/runner.pm</c> at tag <c>curl-8_21_0</c> runs the
/// command through <c>system</c>): blanks separate words; single quotes keep everything
/// literally; double quotes keep everything but a backslash before <c>$</c>, <c>`</c>,
/// <c>"</c> or <c>\</c>, which stands for that character; outside quotes a backslash stands for
/// the character after it.
/// </summary>
/// <remarks>
/// No expansion, globbing, redirection or pipe is interpreted: runtests' own redirections are
/// added outside the case's command, and the cases' commands use none.
/// </remarks>
public static class UpstreamTestCaseShellWords
{
    /// <summary>
    /// Splits <paramref name="commandLine"/> into words.
    /// </summary>
    /// <param name="commandLine">The command line, newlines already removed.</param>
    /// <returns>The words, quotes removed.</returns>
    /// <exception cref="FormatException">A quote is not closed.</exception>
    public static IReadOnlyList<string> Split(string commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        var words = new List<string>();
        var word = new StringBuilder();
        var inWord = false;
        var index = 0;
        while (index < commandLine.Length)
        {
            var isBlank = commandLine[index] is ' ' or '\t';
            if (isBlank)
            {
                EndWord(words, word, inWord);
                index++;
            }
            else
            {
                index = ReadPiece(commandLine, index, word);
            }

            inWord = !isBlank;
        }

        EndWord(words, word, inWord);

        return words;
    }

    // Reads one quoted run, escape or plain character of a word; returns the index after it.
    private static int ReadPiece(string commandLine, int index, StringBuilder word) =>
        commandLine[index] switch
        {
            '\'' => ReadSingleQuoted(commandLine, index + 1, word),
            '"' => ReadDoubleQuoted(commandLine, index + 1, word),
            '\\' => ReadEscaped(commandLine, index + 1, word),
            var character => Append(word, character, index),
        };

    private static void EndWord(List<string> words, StringBuilder word, bool inWord)
    {
        if (inWord)
        {
            words.Add(word.ToString());
            word.Clear();
        }
    }

    private static int Append(StringBuilder word, char character, int index)
    {
        word.Append(character);

        return index + 1;
    }

    private static int ReadSingleQuoted(string commandLine, int start, StringBuilder word)
    {
        var end = commandLine.IndexOf('\'', start);
        if (end < 0)
        {
            throw new FormatException($"A single quote at {start - 1} is not closed: {commandLine}");
        }

        word.Append(commandLine, start, end - start);

        return end + 1;
    }

    private static int ReadDoubleQuoted(string commandLine, int start, StringBuilder word)
    {
        var index = start;
        while (index < commandLine.Length && commandLine[index] != '"')
        {
            index += EscapesInDoubleQuotes(commandLine, index) ? 1 : 0;
            word.Append(commandLine[index]);
            index++;
        }

        return index < commandLine.Length
            ? index + 1
            : throw new FormatException($"A double quote at {start - 1} is not closed: {commandLine}");
    }

    private static bool EscapesInDoubleQuotes(string commandLine, int index) =>
        commandLine[index] == '\\'
        && index + 1 < commandLine.Length
        && "$`\"\\".Contains(commandLine[index + 1], StringComparison.Ordinal);

    // A backslash ending the line stands for itself; there is no next line to join.
    private static int ReadEscaped(string commandLine, int next, StringBuilder word) =>
        next < commandLine.Length
            ? Append(word, commandLine[next], next)
            : Append(word, '\\', next - 1);
}
