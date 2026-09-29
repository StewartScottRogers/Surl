using System.Diagnostics.CodeAnalysis;

namespace Surl.Console;

/// <summary>
/// Checks, before any listener starts, that the served directory can be opened (ADR-0007
/// section 5): a missing path, a file and an unreadable directory all fail here rather than
/// at the first request.
/// </summary>
internal static class ServedDirectoryProbe
{
    /// <summary>
    /// Opens <paramref name="path"/> as a directory and reads its first entry.
    /// </summary>
    /// <param name="path">The served directory, as given with <c>--directory</c>.</param>
    /// <returns><see langword="true"/> when the directory opened and could be read.</returns>
    [ExcludeFromCodeCoverage(Justification = "Asks the disk; covered by the integration tests in Surl.Console.UnitTests.")]
    public static bool CanOpen(string path)
    {
        try
        {
            using var entries = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
            _ = entries.MoveNext();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
