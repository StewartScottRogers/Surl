namespace Surl.Conformance;

/// <summary>
/// The files a test case's run left behind, as the verify checker reads them: the files
/// <c>&lt;file name=...&gt;</c> and <c>&lt;stdout loadfile=...&gt;</c> name and the entries
/// <c>&lt;notexists&gt;</c> lists, behind a seam so the checker is tested without a disk.
/// </summary>
public interface IUpstreamTestCaseRunFiles
{
    /// <summary>
    /// Reads a file's bytes.
    /// </summary>
    /// <param name="path">The path as the case names it, its variables substituted.</param>
    /// <returns>The bytes, or <see langword="null"/> when there is no such file.</returns>
    byte[]? ReadAllBytesOrNull(string path);

    /// <summary>
    /// Reports whether a file or directory exists, as Perl's <c>-e</c> does.
    /// </summary>
    /// <param name="path">The path as the case names it, its variables substituted.</param>
    /// <returns><see langword="true"/> when the entry exists.</returns>
    bool Exists(string path);
}
