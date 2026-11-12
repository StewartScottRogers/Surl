namespace Surl.Conformance;

/// <summary>
/// One name a test case's <c>&lt;server&gt;</c> part may list, and what Surl has for it
/// (ADR-0080 decision 2's table).
/// </summary>
/// <param name="Name">The server name as FILEFORMAT.md spells it, such as <c>http-ipv6</c>.</param>
/// <param name="Protocol">
/// The scheme a pinned build's <c>protocols</c> line must list for curl to talk to the server,
/// such as <c>http</c> for <c>http-ipv6</c> and <c>https-mtls</c>.
/// </param>
/// <param name="IsBuilt">Whether Surl's server half for the name exists today.</param>
/// <param name="UnixSocketPathVariable">
/// The variable, without the <c>%</c>, a case names the server's Unix domain socket path with -
/// <c>HTTPUNIXPATH</c> for <c>http-unix</c>, <c>SOCKSUNIXPATH</c> for <c>socks5unix</c> - or
/// <see langword="null"/> for a server that listens on a port.
/// </param>
/// <param name="PortVariable">
/// The variable, without the <c>%</c>, a case names the port a proxy server listens on with -
/// <c>PROXYPORT</c> for <c>http-proxy</c>, <c>HTTPSPROXYPORT</c> for <c>https-proxy</c> - or
/// <see langword="null"/> for a row whose port variable the run names elsewhere.
/// </param>
public sealed record UpstreamTestCaseServer(
    string Name, string Protocol, bool IsBuilt, string? UnixSocketPathVariable = null, string? PortVariable = null);
