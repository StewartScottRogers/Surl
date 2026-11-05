namespace Surl.Conformance;

/// <summary>
/// Which <c>&lt;servercmd&gt;</c> commands each server half implements, by the first word of the
/// line as the case writes it (ADR-0080 decisions 2 and 3). A case whose main server's half
/// lacks one of its commands is skipped <c>servercmd-unsupported:&lt;command&gt;</c>, and so is a
/// case with a <c>&lt;postcmd&gt;</c>, which no server half implements
/// (<c>servercmd-unsupported:postcmd</c>). A server half that lands adds its row here, the same
/// words its protocol library accepts.
/// </summary>
public static class UpstreamTestCaseServerCommands
{
    /// <summary>
    /// The HTTP server half's commands (BL-391), the same words
    /// <c>Surl.Protocol.Http</c>'s <c>HttpUpstreamTestCaseReplies.SupportedServerCommands</c> accepts.
    /// <c>delay:</c>, <c>stream</c>, <c>rtp:</c>, <c>connection-monitor</c> and <c>upgrade</c> are not.
    /// </summary>
    public static readonly IReadOnlySet<string> Http = new HashSet<string>(
        ["Testnum", "auth_required", "idle", "no-expect", "skip:", "swsclose", "writedelay:"],
        StringComparer.Ordinal);

    /// <summary>The name a case's <c>&lt;postcmd&gt;</c> is checked under; no server half implements it.</summary>
    public const string PostCommand = "postcmd";

    private static readonly Dictionary<string, IReadOnlySet<string>> ByServer = new(StringComparer.Ordinal)
    {
        ["http"] = Http,
        ["http-ipv6"] = Http,
        ["https"] = Http,
        ["https-mtls"] = Http,
    };

    /// <summary>
    /// Whether the server half for <paramref name="server"/> implements <paramref name="command"/>.
    /// </summary>
    /// <param name="server">The <c>&lt;server&gt;</c> name, lower case.</param>
    /// <param name="command">The command's first word, or <see cref="PostCommand"/>.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public static bool IsSupported(string server, string command)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(command);

        return ByServer.TryGetValue(server, out var commands) && commands.Contains(command);
    }
}
