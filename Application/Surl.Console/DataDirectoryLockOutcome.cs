using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// What taking the data directory's <c>.surl/lock</c> file came to (ADR-0031, decision 7):
/// either the open lock, held until <see cref="Holder"/> is disposed, or the exit code and the
/// message, after the <c>surl: </c> prefix, that refuse the start.
/// </summary>
internal sealed class DataDirectoryLockOutcome
{
    private DataDirectoryLockOutcome(IDisposable? holder, SurlExitCode exitCode, string? failureMessage)
    {
        Holder = holder;
        ExitCode = exitCode;
        FailureMessage = failureMessage;
    }

    /// <summary>
    /// The outcome when there is nothing to lock: no <c>--directory</c>, so surl serves in
    /// memory and holds no lock.
    /// </summary>
    public static DataDirectoryLockOutcome NoLock { get; } = new(null, SurlExitCode.Ok, null);

    /// <summary>
    /// The open lock, released when disposed; <see langword="null"/> for
    /// <see cref="NoLock"/> and for a refusal.
    /// </summary>
    public IDisposable? Holder { get; }

    /// <summary>The exit code a refusal ends surl with; <see cref="SurlExitCode.Ok"/> otherwise.</summary>
    public SurlExitCode ExitCode { get; }

    /// <summary>The refusal's message, after the <c>surl: </c> prefix; <see langword="null"/> when the lock is held.</summary>
    public string? FailureMessage { get; }

    /// <summary>The lock is held by <paramref name="holder"/> until it is disposed.</summary>
    /// <param name="holder">The open lock file.</param>
    /// <returns>The outcome.</returns>
    public static DataDirectoryLockOutcome Taken(IDisposable holder) => new(holder, SurlExitCode.Ok, null);

    /// <summary>
    /// Another running surl holds the lock: <see cref="SurlExitCode.DataDirectoryInUse"/> (124).
    /// </summary>
    /// <param name="dataDirectory">The data directory, as given with <c>--directory</c>.</param>
    /// <returns>The refusal.</returns>
    public static DataDirectoryLockOutcome InUse(string dataDirectory) => new(
        null,
        SurlExitCode.DataDirectoryInUse,
        $"(124) Directory {dataDirectory} is in use by another surl process");

    /// <summary>
    /// The <c>.surl</c> folder or the lock file cannot be created:
    /// <see cref="SurlExitCode.CouldNotWriteFile"/> (23).
    /// </summary>
    /// <param name="fullPath">The full path of the folder or the file that could not be created.</param>
    /// <param name="failure">Why; its message goes only to the local operator's stderr.</param>
    /// <returns>The refusal.</returns>
    public static DataDirectoryLockOutcome CouldNotCreate(string fullPath, Exception failure) => new(
        null,
        SurlExitCode.CouldNotWriteFile,
        $"(23) Could not create {fullPath}: {failure.Message}");
}
