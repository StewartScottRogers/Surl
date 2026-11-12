using System.Diagnostics.CodeAnalysis;
using Surl.Cli;
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

    private const string StandardInputPath = "-";

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
    /// Writes the warning for each file <paramref name="commandLine"/> read or will read that may hold
    /// passwords and other users can read: each <see cref="SurlCommandLine.ConfigFiles"/> file in the
    /// order read, then the <see cref="SurlCommandLine.UserFile"/>. Standard input (<c>-K -</c>) has
    /// no file mode of its own and is never warned.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="readMode">Reads a file's Unix mode by its path as given; <see langword="null"/> for none.</param>
    /// <param name="log">The log stream.</param>
    public static void WriteForFilesThatMayHoldPasswords(SurlCommandLine commandLine, Func<string, UnixFileMode?> readMode, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(readMode);
        var files = commandLine.ConfigFiles.Where(file => file != StandardInputPath);
        foreach (var file in commandLine.UserFile is { } userFile ? files.Append(userFile) : files)
        {
            Write(file, readMode(file), commandLine.LogLevel, log);
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
