namespace Surl.Mcp;

/// <summary>
/// What <c>surl-mcp</c> asks of the process, so tests run the host without sockets or changing the test process's
/// current directory.
/// </summary>
/// <param name="CreateServers">Makes the running-server set from the host's settings and standard error.</param>
/// <param name="DirectoryExists">Whether a directory exists, asked of the root.</param>
/// <param name="SetCurrentDirectory">Makes the root the process's current directory.</param>
internal sealed record SurlMcpHostSeams(
    Func<SurlMcpHostSettings, TextWriter, RunningSurlServers> CreateServers,
    Func<string, bool> DirectoryExists,
    Action<string> SetCurrentDirectory)
{
    /// <summary>
    /// Gets the process's seams: servers on real sockets with the system clock, the disk, and
    /// <see cref="Environment.CurrentDirectory"/>.
    /// </summary>
    public static SurlMcpHostSeams Process { get; } = new(
        (settings, error) => new RunningSurlServers(settings, error, TimeProvider.System),
        Directory.Exists,
        directory => Environment.CurrentDirectory = directory);
}
