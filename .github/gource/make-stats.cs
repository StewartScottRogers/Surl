// Writes stats.json, the numbers the Gource viewer page shows, to stdout.
//
// Usage: dotnet run --file make-stats.cs -- <gource log> <width> <height> <fps>
//        (from the repository root)
//
// History numbers (commits, contributors, dates) cover every branch, the same history the
// animation draws. Codebase numbers (projects, C# files and lines, tests) describe the
// checked-out commit the render ran on.
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static Tools;

string[] branches = ["--exclude=gource", "--branches", "--exclude=origin/gource", "--exclude=origin/HEAD", "--remotes"];
var utf8 = new UTF8Encoding(false);

if (args.Length < 4)
{
    Fail("usage: make-stats.cs <gource log> <width> <height> <fps>");
}
string log = args[0];
int width = int.Parse(args[1], CultureInfo.InvariantCulture);
int height = int.Parse(args[2], CultureInfo.InvariantCulture);
int fps = int.Parse(args[3], CultureInfo.InvariantCulture);

List<string[]> rows = [.. SplitLines(File.ReadAllText(log, utf8)).Select(line => line.Split('|'))];
List<string> tracked = SplitLines(Git("ls-files"));
List<string> csFiles = [.. tracked.Where(p => p.EndsWith(".cs", StringComparison.Ordinal))];
long csLines = csFiles.Where(File.Exists).Sum(p => CountLines(File.ReadAllBytes(p)));
var testAttribute = new Regex(@"\[(?:TestMethod|DataTestMethod)\b");
int tests = csFiles
    .Where(p => p.Contains(".UnitTests", StringComparison.Ordinal) && File.Exists(p))
    .Sum(p => testAttribute.Matches(File.ReadAllText(p, utf8)).Count);
var taskDone = new Regex(@"^Tasks/Done/(?:.+/)?BL-\d+.*\.md$");

var stats = new List<(string Key, object Value)>
{
    ("commits", SplitWhitespace(Git(["rev-list", .. branches])).Length),
    ("contributors", rows.Select(r => r[1]).Distinct().Order(StringComparer.Ordinal).ToList()),
    ("firstCommit", long.Parse(rows[0][0], CultureInfo.InvariantCulture)),
    ("lastCommit", long.Parse(rows[^1][0], CultureInfo.InvariantCulture)),
    ("filesTouched", rows.Select(r => r[3]).Distinct().Count()),
    ("projects", tracked.Count(p => p.EndsWith(".csproj", StringComparison.Ordinal))),
    ("csharpFiles", csFiles.Count),
    ("csharpLines", csLines),
    ("tests", tests),
    ("pullRequestsMerged", Regex.Matches(Git(["log", "--merges", "--format=%s", .. branches]), "^Merge pull request #", RegexOptions.Multiline).Count),
    ("tasksDone", tracked.Count(taskDone.IsMatch)),
    ("renderedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
    ("width", width),
    ("height", height),
    ("fps", fps),
};

// The layout Python's json.dump(indent=2) produces, ASCII-only, so the file reads the same
// whichever tool wrote it.
var json = new StringBuilder("{\n");
for (int i = 0; i < stats.Count; i++)
{
    json.Append("  ").Append(Quote(stats[i].Key)).Append(": ");
    if (stats[i].Value is List<string> list)
    {
        if (list.Count == 0)
        {
            json.Append("[]");
        }
        else
        {
            json.Append("[\n").AppendJoin(",\n", list.Select(item => "    " + Quote(item))).Append("\n  ]");
        }
    }
    else
    {
        json.Append(Convert.ToString(stats[i].Value, CultureInfo.InvariantCulture));
    }
    json.Append(i < stats.Count - 1 ? ",\n" : "\n");
}
json.Append("}\n");

using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { NewLine = "\n" };
output.Write(json.ToString());

internal static class Tools
{
    // A JSON string literal escaped as Python's json module does with ensure_ascii.
    internal static string Quote(string value)
    {
        var quoted = new StringBuilder("\"");
        foreach (char c in value)
        {
            quoted.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\b' => "\\b",
                '\f' => "\\f",
                _ when c < ' ' || c > '~' => $"\\u{(int)c:x4}",
                _ => c.ToString(),
            });
        }
        return quoted.Append('"').ToString();
    }

    // Lines in a text file as Python counts them in universal-newline mode: \n, \r\n and a lone
    // \r each end a line, and text after the last line break is one more line.
    internal static long CountLines(byte[] bytes)
    {
        long lines = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == '\n' || bytes[i] == '\r')
            {
                lines++;
                if (bytes[i] == '\r' && i + 1 < bytes.Length && bytes[i + 1] == '\n') i++;
            }
        }
        bool unterminated = bytes.Length > 0 && bytes[^1] != '\n' && bytes[^1] != '\r';
        return lines + (unterminated ? 1 : 0);
    }

    internal static void Fail(string message)
    {
        Console.Error.WriteLine($"make-stats: {message}");
        Environment.Exit(1);
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
            Fail($"git {string.Join(' ', args)} exited with {git.ExitCode}");
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
