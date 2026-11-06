using System.Diagnostics.CodeAnalysis;

namespace Surl.Conformance;

/// <summary>
/// <see cref="IUpstreamCurlFileAccess"/> over the real file system.
/// </summary>
// Excluded from coverage: each member is one call into System.IO, and the fast tests run
// without a disk by design; the locator's logic is covered through a fake of the seam.
[ExcludeFromCodeCoverage]
public sealed class FileSystemUpstreamCurlFileAccess : IUpstreamCurlFileAccess
{
    /// <inheritdoc/>
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public Stream OpenRead(string path) => File.OpenRead(path);
}
