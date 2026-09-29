namespace Surl.Conformance;

/// <summary>
/// One entry of the <c>builds</c> array in <c>UpstreamCurlBuilds.json</c>: an upstream curl
/// build Surl may be measured against, identified by the SHA-256 of its file (ADR-0003).
/// </summary>
/// <param name="Platform">The runtime identifier the build runs on, such as <c>win-x64</c>.</param>
/// <param name="DefaultPath">Where the build's executable is expected on that platform.</param>
/// <param name="Sha256">The pinned SHA-256 of the executable, as hexadecimal text.</param>
/// <param name="Version">The build's <c>curl --version</c> first line, or empty when the entry has none.</param>
/// <param name="Protocols">The protocols the build supports, in the order its entry lists them.</param>
/// <param name="Role">Whether the build is the reference build or a supplementary one.</param>
public sealed record PinnedUpstreamCurlBuild(
    string Platform,
    string DefaultPath,
    string Sha256,
    string Version,
    IReadOnlyList<string> Protocols,
    UpstreamCurlBuildRole Role);
