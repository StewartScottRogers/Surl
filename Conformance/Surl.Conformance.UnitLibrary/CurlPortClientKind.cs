namespace Surl.Conformance;

/// <summary>
/// What file a Curl port pin names, read from the <c>kind</c> field of its entry in
/// <c>CurlPortBuilds.json</c>, so the port's library is never run as a curl and its curl never
/// loaded as the library (ADR-0089 decision 1 (c)). Kept apart from <see cref="UpstreamCurlBuildKind"/>,
/// as every port type is kept apart from upstream's (ADR-0083 decision 2).
/// </summary>
public enum CurlPortClientKind
{
    /// <summary>
    /// The port's curl executable, run as a command. An entry with no <c>kind</c> is a curl.
    /// </summary>
    Curl,

    /// <summary>
    /// The port's managed <c>Libcurl</c> assembly, compiled against and called by the port-side
    /// libcurl drivers (<c>Run-CurlPortLibcurlWebSocketScript.cs</c>, <c>Run-CurlPortLibcurlRtspScript.cs</c>)
    /// and never run as a curl.
    /// </summary>
    Library,
}
