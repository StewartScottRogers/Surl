// Writes a Gource custom log of every branch's history to stdout.
//
// Usage: dotnet run --file make-log.cs    (from the repository root)
//
// Gource on its own reads only HEAD. This walks every local and remote branch except
// the gource media branch, so feature work shows up before it reaches master, and
// prints one "timestamp|author|A/M/D|/path" line per file change, oldest first.
//
// A commit with a Co-authored-by trailer is drawn once for each author, so work Claude
// co-authored shows Claude and the human at the same files. Every Claude model version
// is drawn as one user, "Claude". Runs of whitespace in a name collapse to one space, so a
// typo in git's user.name does not split one person into two.
using System.Diagnostics;
using System.Text;
using static Tools;

string text = Git(
    "log",
    "--exclude=gource", "--branches",
    "--exclude=origin/gource", "--exclude=origin/HEAD", "--remotes",
    "--raw", "--no-renames",
    "--pretty=format:commit|%at|%aN|%(trailers:key=Co-authored-by,valueonly=true,separator=;)");

var rows = new List<(long Stamp, string Author, char Kind, string Path)>();
string? stamp = null;
var authors = new List<string>();
foreach (string line in SplitLines(text))
{
    if (line.StartsWith("commit|", StringComparison.Ordinal))
    {
        string[] fields = line.Split('|', 4);
        stamp = fields[1];
        authors = [DisplayName(fields[2])];
        foreach (string value in fields[3].Split(';'))
        {
            if (value.Any(c => !IsSpace(c)) && !authors.Contains(DisplayName(value)))
            {
                authors.Add(DisplayName(value));
            }
        }
    }
    else if (line.StartsWith(':') && !string.IsNullOrEmpty(stamp))
    {
        int tab = line.IndexOf('\t');
        string meta = line[..tab];
        string path = line[(tab + 1)..];
        char status = SplitWhitespace(meta)[4][0];
        char kind = status is 'A' or 'D' ? status : 'M';
        foreach (string author in authors)
        {
            rows.Add((long.Parse(stamp), author, kind, path));
        }
    }
}

// OrderBy is a stable sort, as Python's list.sort is: changes at one second keep git's order.
using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { NewLine = "\n" };
foreach (var row in rows.OrderBy(r => r.Stamp))
{
    output.Write($"{row.Stamp}|{row.Author}|{row.Kind}|/{row.Path}\n");
}

internal static class Tools
{
    // The Gource user name for a git author or co-author value.
    internal static string DisplayName(string raw)
    {
        string name = string.Join(' ', SplitWhitespace(raw.Split('<', 2)[0]));
        return name.StartsWith("Claude", StringComparison.Ordinal) ? "Claude" : name;
    }

    // Runs git in the current directory and returns its standard output; exits on failure.
    internal static string Git(params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            UseShellExecute = false,
        };
        foreach (string arg in args)
        {
            start.ArgumentList.Add(arg);
        }
        using Process git = Process.Start(start)!;
        Task<string> error = git.StandardError.ReadToEndAsync();
        string result = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0)
        {
            Console.Error.Write(error.Result);
            Console.Error.WriteLine($"make-log: git {string.Join(' ', args)} exited with {git.ExitCode}");
            Environment.Exit(1);
        }
        return result;
    }

    // Python's str.split() with no argument: fields separated by runs of whitespace.
    internal static string[] SplitWhitespace(string value)
    {
        var fields = new List<string>();
        int i = 0;
        while (i < value.Length)
        {
            while (i < value.Length && IsSpace(value[i])) i++;
            int begin = i;
            while (i < value.Length && !IsSpace(value[i])) i++;
            if (i > begin) fields.Add(value[begin..i]);
        }
        return [.. fields];
    }

    // Python's str.isspace for one character: .NET's set plus the four information separators.
    internal static bool IsSpace(char c) => char.IsWhiteSpace(c) || c is >= '\x1c' and <= '\x1f';

    // Python's str.splitlines(): every line boundary Python recognises, no trailing empty line.
    internal static List<string> SplitLines(string value)
    {
        var lines = new List<string>();
        int begin = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c is '\n' or '\r' or '\v' or '\f' or '\x1c' or '\x1d' or '\x1e' or '\x85' or (char)0x2028 or (char)0x2029)
            {
                lines.Add(value[begin..i]);
                if (c == '\r' && i + 1 < value.Length && value[i + 1] == '\n') i++;
                begin = i + 1;
            }
        }
        if (begin < value.Length) lines.Add(value[begin..]);
        return lines;
    }
}
