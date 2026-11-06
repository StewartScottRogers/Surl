namespace Surl.Conformance;

/// <summary>
/// Reads a test case run's files from the file system.
/// </summary>
public sealed class FileSystemUpstreamTestCaseRunFiles : IUpstreamTestCaseRunFiles
{
    /// <inheritdoc/>
    public byte[]? ReadAllBytesOrNull(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

    /// <inheritdoc/>
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
