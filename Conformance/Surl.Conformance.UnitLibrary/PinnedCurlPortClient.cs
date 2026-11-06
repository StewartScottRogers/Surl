namespace Surl.Conformance;

/// <summary>
/// One entry of the <c>builds</c> array in <c>CurlPortBuilds.json</c>: a published Curl port
/// client an extended server's integration test may connect with, identified by the SHA-256 of
/// its file (ADR-0083 decision 2). It is a live client only, never an oracle, and never an
/// upstream curl build.
/// </summary>
/// <param name="Platform">The runtime identifier the client runs on, such as <c>win-x64</c>.</param>
/// <param name="Path">
/// Where the client's executable is expected on that platform, as the file writes it; environment
/// variables written <c>%NAME%</c>, such as <c>%LOCALAPPDATA%</c>, are expanded when it is located.
/// </param>
/// <param name="Sha256">The pinned SHA-256 of the executable, as hexadecimal text.</param>
/// <param name="Commit">The Curl port's <c>master</c> commit the executable was published from.</param>
/// <param name="PublishCommand">The exact <c>dotnet publish</c> command line that produced it.</param>
/// <param name="Published">The date it was published, as the file writes it.</param>
public sealed record PinnedCurlPortClient(
    string Platform,
    string Path,
    string Sha256,
    string Commit,
    string PublishCommand,
    string Published)
{
    /// <summary>
    /// Gets what the pinned file is: the port's curl executable (the default, for an entry with no
    /// <c>kind</c>) or its managed <c>Libcurl</c> assembly (ADR-0089 decision 1 (c)).
    /// </summary>
    public CurlPortClientKind Kind { get; init; } = CurlPortClientKind.Curl;
}
