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
/// <param name="IsBuilt">
/// Whether <see cref="UpstreamTestCaseSelector"/> lets a case naming the server past its
/// <c>server-not-built:&lt;name&gt;</c> skip (ADR-0080 decision 3). <see langword="true"/> does not
/// by itself mean the upstream test-case run composes a server half: the <c>rtsp</c> and
/// <c>rtsp-ipv6</c> rows are <see langword="true"/>, and <c>Surl.Conformance.UnitTests</c>'
/// <c>UpstreamTestCasesAgainstSurlTests</c> composes none for them, every <c>rtsp</c> case at tag
/// <c>curl-8_21_0</c> being a libtest program (ADR-0080's 2026-11-27 amendment).
/// </param>
/// <param name="UnixSocketPathVariable">
/// The variable, without the <c>%</c>, a case names the server's Unix domain socket path with -
/// <c>HTTPUNIXPATH</c> for <c>http-unix</c>, <c>SOCKSUNIXPATH</c> for <c>socks5unix</c> - or
/// <see langword="null"/> for a server that listens on a port.
/// </param>
/// <param name="PortVariable">
/// The variable, without the <c>%</c>, a case names the port a server listens on with -
/// <c>PROXYPORT</c> for <c>http-proxy</c>, <c>HTTPTLSPORT</c> for <c>httptls+srp</c> - or
/// <see langword="null"/> for a row whose port variable the run names elsewhere.
/// </param>
public sealed record UpstreamTestCaseServer(
    string Name, string Protocol, bool IsBuilt, string? UnixSocketPathVariable = null, string? PortVariable = null);
