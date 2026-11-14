using System.Globalization;

namespace Surl.Protocol.Http;

/// <summary>
/// Reads the <c>&lt;reply&gt;&lt;postcmd&gt;</c> lines of an upstream test case for the HTTP server
/// half: FILEFORMAT.md at tag <c>curl-8_21_0</c> gives HTTP and HTTPS one, <c>wait [secs]</c>.
/// </summary>
internal static class HttpUpstreamTestCasePostCommands
{
    /// <summary>
    /// The pause the lines ask for after each reply; blank and <c>#</c> lines are skipped and the last
    /// <c>wait</c> wins.
    /// </summary>
    /// <param name="lines">The <c>&lt;postcmd&gt;</c> lines.</param>
    /// <returns>The pause; zero when there is no <c>wait</c>.</returns>
    /// <exception cref="NotSupportedException">A line is not <c>wait</c>.</exception>
    /// <exception cref="FormatException">A <c>wait</c> line has no non-negative number of seconds.</exception>
    public static TimeSpan ParseWait(IEnumerable<string> lines) =>
        lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Aggregate(TimeSpan.Zero, (_, line) => Wait(line));

    private static TimeSpan Wait(string line)
    {
        var words = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (words[0] != "wait")
        {
            throw new NotSupportedException($"The HTTP server half does not implement the postcmd '{words[0]}' (servercmd-unsupported:postcmd).");
        }

        return words.Length > 1 && double.TryParse(words[1].Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : throw new FormatException($"The postcmd '{line}' needs a non-negative number of seconds.");
    }
}
