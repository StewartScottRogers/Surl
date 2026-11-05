namespace Surl.Conformance;

/// <summary>
/// Reads the files a test case's <c>%include</c>, <c>%includetext</c>,
/// <c>%sha256b64file[]</c> and <c>%strippemfile[]</c> instructions name, behind a seam so the
/// preprocessor is tested with no disk.
/// </summary>
public interface IUpstreamTestCaseFileReader
{
    /// <summary>
    /// Reads every byte of the file at <paramref name="path"/>, as the instruction names it
    /// after variable substitution.
    /// </summary>
    /// <param name="path">The path the instruction names.</param>
    /// <returns>The file's bytes.</returns>
    /// <exception cref="IOException">The file cannot be read.</exception>
    byte[] ReadAllBytes(string path);
}
