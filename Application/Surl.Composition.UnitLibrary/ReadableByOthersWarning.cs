using System.Diagnostics.CodeAnalysis;
using Surl.Output;

namespace Surl.Composition;

/// <summary>
/// The warning a file that may hold passwords writes at start when its Unix mode lets other users
/// read it (ADR-0101, decision 5): <c>surl: warning: &lt;file&gt; is readable by other users and may
/// hold passwords</c>, from the info level up with the other start-up warnings (ADR-0032 section 9,
/// ADR-0033). Windows reports no mode, so no warning is written there.
/// </summary>
/// <remarks>
/// Public because more than one host calls it, but not a supported programming interface: it may
/// change in any release (ADR-0099, decision 1).
/// </remarks>
public static class ReadableByOthersWarning
{
    private const UnixFileMode ReadByOthers = UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    /// <summary>
    /// Writes the warning for <paramref name="file"/> when <paramref name="mode"/> grants group or
    /// other read and <paramref name="logLevel"/> is info or louder; writes nothing otherwise.
    /// </summary>
    /// <param name="file">The file as given on the command line.</param>
    /// <param name="mode">The file's Unix mode; <see langword="null"/> on Windows or when it cannot be read.</param>
    /// <param name="logLevel">The run's log level.</param>
    /// <param name="log">The log stream.</param>
    public static void Write(string file, UnixFileMode? mode, LogLevel logLevel, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (logLevel >= LogLevel.Info && mode is { } given && (given & ReadByOthers) != 0)
        {
            log.WriteLine($"surl: warning: {file} is readable by other users and may hold passwords");
        }
    }

    /// <summary>
    /// Reads <paramref name="path"/>'s Unix mode from the disk: <see langword="null"/> on Windows,
    /// whose ACLs have no equivalent the base class library reads simply, and when the mode cannot
    /// be read.
    /// </summary>
    /// <param name="path">The file's path as given.</param>
    /// <returns>The mode, or <see langword="null"/>.</returns>
    [ExcludeFromCodeCoverage(Justification = "Calls File.GetUnixFileMode off Windows; covered by ReadableByOthersWarningTests on Linux and macOS.")]
    public static UnixFileMode? ReadDiskMode(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return File.GetUnixFileMode(path);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
