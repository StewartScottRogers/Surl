namespace Surl.Conformance;

/// <summary>
/// The file-system access <see cref="UpstreamCurlLocator"/> needs to find and hash an
/// upstream curl build, behind a seam so the locator is tested with no disk.
/// </summary>
public interface IUpstreamCurlFileAccess
{
    /// <summary>
    /// Reports whether a file exists at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns><see langword="true"/> when the file exists.</returns>
    bool FileExists(string path);

    /// <summary>
    /// Opens the file at <paramref name="path"/> for reading.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>A readable stream the caller disposes.</returns>
    Stream OpenRead(string path);
}
