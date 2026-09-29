using System.Diagnostics.CodeAnalysis;

namespace Surl.Console;

/// <summary>
/// Takes the data directory's lock (ADR-0031, decision 7): <c>&lt;path&gt;/.surl/lock</c>,
/// held open exclusively for as long as surl serves, so a second surl given the same path is
/// refused before any listener binds. The operating system releases the lock when its holder
/// exits or is killed, so a lock file left on disk refuses nothing; the file is never deleted.
/// </summary>
internal static class DataDirectoryLock
{
    /// <summary>The folder under the data directory that holds surl's own state.</summary>
    public const string StateFolderName = ".surl";

    /// <summary>The lock file's name inside <see cref="StateFolderName"/>.</summary>
    public const string LockFileName = "lock";

    /// <summary>
    /// Creates <c>&lt;path&gt;/.surl</c> if missing, then opens <c>.surl/lock</c> with
    /// <see cref="FileMode.OpenOrCreate"/>, <see cref="FileAccess.ReadWrite"/> and
    /// <see cref="FileShare.None"/>: a share-mode lock on Windows, an advisory <c>flock</c> on
    /// Linux and macOS, both refusing a second open from any process, this one included.
    /// </summary>
    /// <param name="dataDirectory">The data directory, as given with <c>--directory</c>.</param>
    /// <returns>
    /// The open lock; <see cref="DataDirectoryLockOutcome.InUse"/> when another holder has it;
    /// <see cref="DataDirectoryLockOutcome.CouldNotCreate"/> when the folder cannot be created
    /// or the file cannot be opened for lack of permission.
    /// </returns>
    [ExcludeFromCodeCoverage(Justification = "Asks the disk; covered by the integration tests in DataDirectoryLockTests.")]
    public static DataDirectoryLockOutcome Take(string dataDirectory)
    {
        var stateFolder = Path.Join(Path.GetFullPath(dataDirectory), StateFolderName);
        try
        {
            Directory.CreateDirectory(stateFolder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DataDirectoryLockOutcome.CouldNotCreate(stateFolder, exception);
        }

        var lockFile = Path.Join(stateFolder, LockFileName);
        try
        {
            return DataDirectoryLockOutcome.Taken(
                new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (UnauthorizedAccessException exception)
        {
            return DataDirectoryLockOutcome.CouldNotCreate(lockFile, exception);
        }
        catch (IOException)
        {
            return DataDirectoryLockOutcome.InUse(dataDirectory);
        }
    }
}
