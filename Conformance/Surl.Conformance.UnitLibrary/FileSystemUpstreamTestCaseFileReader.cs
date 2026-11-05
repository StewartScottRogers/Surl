namespace Surl.Conformance;

/// <summary>
/// Reads a test case's included files from the file system, relative paths resolved against
/// the process's current directory as upstream's runtests resolves them against its own.
/// </summary>
public sealed class FileSystemUpstreamTestCaseFileReader : IUpstreamTestCaseFileReader
{
    /// <inheritdoc/>
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
}
