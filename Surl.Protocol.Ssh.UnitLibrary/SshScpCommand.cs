using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// An <c>exec</c> command that means SCP (ADR-0054, decision 2):
/// <c>scp &lt;options&gt; [--] &lt;path word&gt;</c>, the options drawn from <c>f</c>, <c>t</c>,
/// <c>p</c>, <c>d</c> and <c>v</c> with exactly one of <c>f</c> and <c>t</c>, and the path one
/// word quoted as <see cref="SshCommandWords"/> reads it.
/// </summary>
/// <param name="IsSource"><c>-f</c>: surl sends the file (curl's download); otherwise <c>-t</c>, surl receives it.</param>
/// <param name="PreservesTimes"><c>-p</c>: the <c>T</c> line is sent (<c>-f</c>) or honoured (<c>-t</c>).</param>
/// <param name="TargetIsDirectory"><c>-d</c>: a <c>-t</c> target must be a directory.</param>
/// <param name="Path">The path word, quotes removed.</param>
internal sealed record SshScpCommand(bool IsSource, bool PreservesTimes, bool TargetIsDirectory, string Path)
{
    private const string OptionLetters = "ftpdv";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads <paramref name="command"/> as an SCP command.
    /// </summary>
    /// <param name="command">The <c>exec</c> request's command bytes.</param>
    /// <param name="maxLineBytes">The most bytes the command may hold (<c>--max-line</c>); 0 means no limit.</param>
    /// <param name="refusal">
    /// Why the command is not one surl serves - <c>not an scp command</c>, <c>-r is not served</c>,
    /// <c>unknown option -&lt;c&gt;</c>, <c>needs exactly one path</c>, <c>unquoted &lt;c&gt;</c>,
    /// <c>not UTF-8</c> or <c>past --max-line</c> - or empty when it is one.
    /// </param>
    /// <returns>The command, or <see langword="null"/> when it was refused.</returns>
    public static SshScpCommand? Parse(ReadOnlySpan<byte> command, long maxLineBytes, out string refusal)
    {
        if (maxLineBytes > 0 && command.Length > maxLineBytes)
        {
            refusal = "past --max-line";

            return null;
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(command);
        }
        catch (DecoderFallbackException)
        {
            refusal = "not UTF-8";

            return null;
        }

        var words = SshCommandWords.Split(text, out refusal);

        return words is null ? null : FromWords(words, out refusal);
    }

    private static SshScpCommand? FromWords(List<string> words, out string refusal)
    {
        if (words.Count == 0 || words[0] != "scp")
        {
            refusal = "not an scp command";

            return null;
        }

        var (letters, pathIndex) = ReadOptions(words);
        refusal = RefusalOfOptions(letters) ?? RefusalOfPaths(words.Count - pathIndex);

        return refusal.Length > 0 ? null : FromOptions(letters, words[pathIndex]);
    }

    // The option letters of the words after scp, and where the paths start, past any "--".
    private static (string Letters, int PathIndex) ReadOptions(List<string> words)
    {
        var index = 1;
        var letters = new StringBuilder();
        while (index < words.Count && IsOptionWord(words[index]))
        {
            letters.Append(words[index], 1, words[index].Length - 1);
            index++;
        }

        var pathIndex = index < words.Count && words[index] == "--" ? index + 1 : index;

        return (letters.ToString(), pathIndex);
    }

    private static string RefusalOfPaths(int pathCount) => pathCount == 1 ? string.Empty : "needs exactly one path";

    private static bool IsOptionWord(string word) => word.Length > 1 && word[0] == '-' && word != "--";

    private static string? RefusalOfOptions(string letters)
    {
        if (letters.Contains('r', StringComparison.Ordinal))
        {
            return "-r is not served";
        }

        foreach (var letter in letters.Where(letter => !OptionLetters.Contains(letter, StringComparison.Ordinal)))
        {
            return $"unknown option -{letter}";
        }

        return letters.Count(letter => letter is 'f' or 't') == 1 ? null : "not an scp command";
    }

    private static SshScpCommand FromOptions(string letters, string path) => new(
        letters.Contains('f', StringComparison.Ordinal),
        letters.Contains('p', StringComparison.Ordinal),
        letters.Contains('d', StringComparison.Ordinal),
        path);
}
