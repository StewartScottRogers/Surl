// Writes a Gource caption file of Surl's milestones to stdout.
//
// Usage: dotnet run --file make-captions.cs    (from the repository root)
//
// One "timestamp|text" line per milestone, oldest first: every merged pull request (from
// the merge commit's subject and title line) and every version tag. Gource shows each
// caption as the animation reaches its moment.
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using static Tools;

var captions = new SortedDictionary<long, string>();
string merges = Git(
    "log", "--merges", "--exclude=gource", "--branches",
    "--exclude=origin/gource", "--exclude=origin/HEAD", "--remotes",
    "--pretty=format:%at%x1f%s%x1f%b%x1e");
foreach (string record in merges.Split('\x1e'))
{
    string[] parts = record.Trim('\n').Split('\x1f');
    if (parts.Length < 3)
    {
        continue;
    }
    if (parts.Length > 3)
    {
        Fail($"a merge record has {parts.Length} fields, expected 3");
    }
    Match pr = Regex.Match(parts[1], @"^Merge pull request #(\d+)");
    if (!pr.Success)
    {
        continue;
    }
    string title = SplitLines(parts[2]).Select(Strip).FirstOrDefault(line => line.Length > 0) ?? "";
    captions[long.Parse(parts[0])] = $"Pull request #{pr.Groups[1].Value} merged: {title}".TrimEnd(':', ' ');
}

string tags = Git("for-each-ref", "refs/tags", "--format=%(creatordate:unix)%1f%(refname:short)");
foreach (string line in SplitLines(tags))
{
    string[] parts = line.Split('\x1f');
    if (parts.Length != 2)
    {
        Fail($"a tag line has {parts.Length} fields, expected 2");
    }
    if (parts[1].StartsWith('v'))
    {
        captions[long.Parse(parts[0]) + 1] = $"Released {parts[1]}";
    }
}

using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { NewLine = "\n" };
foreach (var (stamp, caption) in captions)
{
    output.Write($"{stamp}|{caption}\n");
}

internal static class Tools
{
    internal static void Fail(string message)
    {
        Console.Error.WriteLine($"make-captions: {message}");
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

    // Python's str.strip() with no argument: .NET's whitespace plus the four information separators.
    internal static string Strip(string value)
    {
        int begin = 0, end = value.Length;
        while (begin < end && IsSpace(value[begin])) begin++;
        while (end > begin && IsSpace(value[end - 1])) end--;
        return value[begin..end];
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
