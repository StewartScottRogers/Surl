using System.Globalization;

namespace Surl.Protocol.Http;

/// <summary>
/// The <c>&lt;servercmd&gt;</c> lines of an upstream test case that the HTTP server half
/// implements, read once (FILEFORMAT.md at tag <c>curl-8_21_0</c>, "For HTTP/HTTPS"; ADR-0080
/// decision 2).
/// </summary>
/// <param name="AuthRequired">
/// <c>auth_required</c>: a <c>POST</c> or <c>PUT</c> without <c>Authorization</c> is answered
/// without its body being read.
/// </param>
/// <param name="Idle"><c>idle</c>: after reading a request, send nothing and wait for the client to close.</param>
/// <param name="NoExpect"><c>no-expect</c>: a request carrying <c>Expect</c> is answered without its body being read.</param>
/// <param name="ClosesAfterReply"><c>swsclose</c>: the connection is closed after every reply.</param>
/// <param name="WriteDelay"><c>writedelay: [msecs]</c>: the pause between the reply's packets.</param>
/// <param name="SkippedBodyBytes"><c>skip: [num]</c>: how many bytes of a <c>POST</c> or <c>PUT</c> body are left unread.</param>
internal sealed record HttpUpstreamTestCaseServerCommands(
    bool AuthRequired,
    bool Idle,
    bool NoExpect,
    bool ClosesAfterReply,
    TimeSpan WriteDelay,
    long SkippedBodyBytes)
{
    // What each command does to the commands read so far, given its line and its argument.
    private static readonly Dictionary<string, Func<HttpUpstreamTestCaseServerCommands, string, string, HttpUpstreamTestCaseServerCommands>> Appliers = new(StringComparer.Ordinal)
    {
        ["Testnum"] = static (commands, _, _) => commands,
        ["auth_required"] = static (commands, _, _) => commands with { AuthRequired = true },
        ["idle"] = static (commands, _, _) => commands with { Idle = true },
        ["no-expect"] = static (commands, _, _) => commands with { NoExpect = true },
        ["swsclose"] = static (commands, _, _) => commands with { ClosesAfterReply = true },
        ["writedelay:"] = static (commands, line, argument) => commands with { WriteDelay = TimeSpan.FromMilliseconds(Number(line, argument)) },
        ["skip:"] = static (commands, line, argument) => commands with { SkippedBodyBytes = Number(line, argument) },
    };

    /// <summary>
    /// The first words of the lines <see cref="Parse"/> accepts. <c>Testnum</c> is the line
    /// upstream's runtests writes first; it names the case, which a server half already knows.
    /// </summary>
    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(Appliers.Keys, StringComparer.Ordinal);

    private static readonly HttpUpstreamTestCaseServerCommands None = new(false, false, false, false, TimeSpan.Zero, 0);

    /// <summary>
    /// Reads <paramref name="lines"/>; blank lines and lines starting <c>#</c> are skipped.
    /// </summary>
    /// <param name="lines">The case's <c>&lt;servercmd&gt;</c> lines.</param>
    /// <returns>The commands.</returns>
    /// <exception cref="NotSupportedException">A line names a command the server half does not implement.</exception>
    /// <exception cref="FormatException">A <c>writedelay:</c> or <c>skip:</c> line has no non-negative number.</exception>
    public static HttpUpstreamTestCaseServerCommands Parse(IEnumerable<string> lines) =>
        lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Aggregate(None, Apply);

    private static HttpUpstreamTestCaseServerCommands Apply(HttpUpstreamTestCaseServerCommands commands, string line)
    {
        var words = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        var argument = words.Length > 1 ? words[1] : string.Empty;

        return Appliers.TryGetValue(words[0], out var apply)
            ? apply(commands, line, argument)
            : throw new NotSupportedException($"The HTTP server half does not implement the server command '{words[0]}' (servercmd-unsupported:{words[0]}).");
    }

    private static long Number(string line, string argument) =>
        long.TryParse(argument.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new FormatException($"The server command '{line}' needs a non-negative whole number.");
}
