namespace Surl.Conformance;

/// <summary>
/// What file a pinned upstream curl entry names, read from the <c>kind</c> field of its entry
/// in <c>UpstreamCurlBuilds.json</c>, so a library is never run as a curl and a curl never
/// loaded as the library (ADR-0071 decision 10).
/// </summary>
public enum UpstreamCurlBuildKind
{
    /// <summary>
    /// A curl executable, run as a command. An entry with no <c>kind</c> is a curl.
    /// </summary>
    Curl,

    /// <summary>
    /// A shared libcurl, such as <c>libcurl-4.dll</c>, loaded by a program that drives
    /// libcurl's API (<c>curl_ws_send</c>, <c>curl_ws_recv</c>) and never run as a curl.
    /// </summary>
    Library,
}
