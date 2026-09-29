using System.Diagnostics.CodeAnalysis;

namespace Surl.Console;

/// <summary>
/// Opens a <c>--log-file</c>, <c>--trace</c> or <c>--trace-ascii</c> file on disk (ADR-0033,
/// sections 4 and 6): the one place <c>surl</c> writes a log to a real file.
/// </summary>
internal static class LogFile
{
    /// <summary>
    /// Opens <paramref name="path"/> for writing, UTF-8 without a byte order mark, flushing every
    /// write so a reader following the file sees each line at once. Others may read it meanwhile.
    /// </summary>
    /// <param name="path">The file, as given on the command line.</param>
    /// <param name="mode">
    /// <see cref="FileMode.Append"/> for <c>--log-file</c> (appended, created if missing),
    /// <see cref="FileMode.Create"/> for a trace file (truncated, created if missing).
    /// </param>
    /// <returns>The writer; disposing it closes the file.</returns>
    /// <exception cref="IOException">The file cannot be opened.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be written.</exception>
    [ExcludeFromCodeCoverage(Justification = "Opens a file on disk; covered by the integration tests in LogFileTests.")]
    internal static TextWriter Open(string path, FileMode mode) =>
        new StreamWriter(new FileStream(path, mode, FileAccess.Write, FileShare.Read | FileShare.Delete)) { AutoFlush = true };
}
