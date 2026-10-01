namespace Surl.Conformance;

/// <summary>
/// Thrown when a curl executable's or libcurl's SHA-256 does not match a pin of its kind in
/// <c>UpstreamCurlBuilds.json</c>, so it is not a build Surl may be measured against
/// (ADR-0003). The message is the one <c>Assert-PinnedUpstreamCurl</c> in
/// <c>Record-CurlExchange.ps1</c> gives.
/// </summary>
public sealed class UnpinnedUpstreamCurlException : Exception
{
    /// <summary>
    /// Initializes a new instance naming the refused curl executable and its hash.
    /// </summary>
    /// <param name="path">The path of the refused executable.</param>
    /// <param name="sha256">The executable's SHA-256, as upper-case hexadecimal text.</param>
    public UnpinnedUpstreamCurlException(string path, string sha256)
        : this(path, sha256, UpstreamCurlBuildKind.Curl)
    {
    }

    /// <summary>
    /// Initializes a new instance naming the refused file, its hash and the kind of pin it
    /// was checked against.
    /// </summary>
    /// <param name="path">The path of the refused file.</param>
    /// <param name="sha256">The file's SHA-256, as upper-case hexadecimal text.</param>
    /// <param name="kind">Whether the file was to be run as a curl or loaded as libcurl.</param>
    public UnpinnedUpstreamCurlException(string path, string sha256, UpstreamCurlBuildKind kind)
        : base($"{path} (SHA-256 {sha256}) is not a pinned {KindName(kind)}. Surl is measured only against the builds in UpstreamCurlBuilds.json (ADR-0003); pinning another is a decision, and the Curl port is never one.")
    {
        Path = path;
        Sha256 = sha256;
        Kind = kind;
    }

    /// <summary>
    /// Gets the path of the refused file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the refused file's SHA-256, as upper-case hexadecimal text.
    /// </summary>
    public string Sha256 { get; }

    /// <summary>
    /// Gets the kind of pin the file was checked against.
    /// </summary>
    public UpstreamCurlBuildKind Kind { get; }

    /// <summary>
    /// Names a kind of pin the way a refusal does: <c>upstream curl build</c> or
    /// <c>upstream libcurl</c>.
    /// </summary>
    internal static string KindName(UpstreamCurlBuildKind kind) =>
        kind == UpstreamCurlBuildKind.Library ? "upstream libcurl" : "upstream curl build";
}
