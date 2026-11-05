namespace Surl.Mcp;

/// <summary>What a <c>surl-mcp</c> command line asks for: serve with settings, show the help, show the version, or a refusal.</summary>
/// <param name="Action">What to do.</param>
/// <param name="Settings">The host's settings, when <paramref name="Action"/> is <see cref="SurlMcpCommandLineAction.Serve"/>.</param>
/// <param name="Refusal">The line written to standard error, when <paramref name="Action"/> is <see cref="SurlMcpCommandLineAction.Refuse"/>.</param>
internal sealed record SurlMcpCommandLineResult(SurlMcpCommandLineAction Action, SurlMcpHostSettings? Settings, string? Refusal)
{
    /// <summary>Gets the answer to <c>--help</c>.</summary>
    public static SurlMcpCommandLineResult Help { get; } = new(SurlMcpCommandLineAction.ShowHelp, null, null);

    /// <summary>Gets the answer to <c>--version</c>.</summary>
    public static SurlMcpCommandLineResult Version { get; } = new(SurlMcpCommandLineAction.ShowVersion, null, null);

    /// <summary>Serve with <paramref name="settings"/>.</summary>
    /// <param name="settings">The host's settings.</param>
    /// <returns>The result.</returns>
    public static SurlMcpCommandLineResult Serve(SurlMcpHostSettings settings) => new(SurlMcpCommandLineAction.Serve, settings, null);

    /// <summary>Refuse the command line with <paramref name="refusal"/>.</summary>
    /// <param name="refusal">The line written to standard error.</param>
    /// <returns>The result.</returns>
    public static SurlMcpCommandLineResult Refuse(string refusal) => new(SurlMcpCommandLineAction.Refuse, null, refusal);
}
