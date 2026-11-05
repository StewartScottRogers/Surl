namespace Surl.Conformance;

/// <summary>
/// Thrown when a Curl port client's SHA-256 does not match its pin in <c>CurlPortBuilds.json</c>,
/// or matches an upstream curl build pinned in <c>UpstreamCurlBuilds.json</c>, so it is not a
/// client an extended server's integration test may run (ADR-0083 decision 2).
/// </summary>
/// <remarks>
/// Its own type, never an <see cref="UnpinnedUpstreamCurlException"/>: a refused port client
/// and a refused upstream curl are different failures with different remedies.
/// </remarks>
public sealed class UnpinnedCurlPortClientException : Exception
{
    /// <summary>
    /// Initializes a new instance naming the refused file and its hash.
    /// </summary>
    /// <param name="path">The path of the refused file.</param>
    /// <param name="sha256">The file's SHA-256, as upper-case hexadecimal text.</param>
    public UnpinnedCurlPortClientException(string path, string sha256)
        : base($"{path} (SHA-256 {sha256}) is not a pinned Curl port client. An extended server's integration test runs only a client pinned in CurlPortBuilds.json (ADR-0083); publish the pinned commit with its recorded command, or pin the new build.")
    {
        Path = path;
        Sha256 = sha256;
    }

    /// <summary>
    /// Gets the path of the refused file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the refused file's SHA-256, as upper-case hexadecimal text.
    /// </summary>
    public string Sha256 { get; }
}
