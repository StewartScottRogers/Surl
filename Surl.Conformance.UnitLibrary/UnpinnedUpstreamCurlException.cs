namespace Surl.Conformance;

/// <summary>
/// Thrown when a curl executable's SHA-256 does not match its pin in
/// <c>UpstreamCurlBuilds.json</c>, so it is not a build Surl may be measured against
/// (ADR-0003). The message is the one <c>Assert-PinnedUpstreamCurl</c> in
/// <c>Record-CurlExchange.ps1</c> gives.
/// </summary>
public sealed class UnpinnedUpstreamCurlException : Exception
{
    /// <summary>
    /// Initializes a new instance naming the refused file and its hash.
    /// </summary>
    /// <param name="path">The path of the refused executable.</param>
    /// <param name="sha256">The executable's SHA-256, as upper-case hexadecimal text.</param>
    public UnpinnedUpstreamCurlException(string path, string sha256)
        : base($"{path} (SHA-256 {sha256}) is not a pinned upstream curl build. Surl is measured only against the builds in UpstreamCurlBuilds.json (ADR-0003); pinning another is a decision, and the Curl port is never one.")
    {
        Path = path;
        Sha256 = sha256;
    }

    /// <summary>
    /// Gets the path of the refused executable.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the refused executable's SHA-256, as upper-case hexadecimal text.
    /// </summary>
    public string Sha256 { get; }
}
