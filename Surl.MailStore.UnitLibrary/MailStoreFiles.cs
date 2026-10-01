using System.Globalization;
using Surl.Content;

namespace Surl.MailStore;

/// <summary>
/// The files that keep a <see cref="MailboxStore"/> across restarts, read and written through
/// <see cref="IContentFileSystem"/> (ADR-0050, decision 7): the index, rewritten whole after
/// every change, and one file per distinct message, written once.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where.</b> <see cref="IndexFileName"/> in the state folder it is given,
/// <c>&lt;data directory&gt;/.surl/mail</c> when surl serves with <c>--directory</c>, and each
/// message in <see cref="MessagesFolderName"/> beside it, named by its message file number as
/// 16 lower-case hexadecimal digits. Folders are created, with every missing folder above them,
/// before each write.
/// </para>
/// <para>
/// <b>How it is written.</b> The index to <c>.index-&lt;guid&gt;</c> beside it, then renamed
/// over the index; a message body is streamed into <c>messages/.pending-&lt;guid&gt;</c> as a
/// server reads it, then renamed to its number when the delivery or append is stored. A failure
/// mid-write leaves the file as it was and never a partial one: an index write that throws
/// deletes its temporary file before the exception is rethrown, and a pending file not stored
/// is deleted; one left by a crash is ignored at load.
/// </para>
/// <para>
/// <b>How it is read.</b> The index at load; a message file's bytes only when a server fetches
/// the message, never held in memory.
/// </para>
/// </remarks>
public sealed class MailStoreFiles
{
    /// <summary>
    /// The index's name in its state folder: <c>index</c>.
    /// </summary>
    public const string IndexFileName = "index";

    /// <summary>
    /// The name of the folder beside the index that holds the message files: <c>messages</c>.
    /// </summary>
    public const string MessagesFolderName = "messages";

    /// <summary>
    /// The reason an index that does not parse is refused with: <c>not a mail store index</c>.
    /// </summary>
    public const string MalformedIndexReason = "not a mail store index";

    /// <summary>
    /// The reason a message file the index names is refused with when it is missing or not the
    /// size the index gives: <c>missing or not the size the index gives</c>.
    /// </summary>
    public const string MismatchedMessageFileReason = "missing or not the size the index gives";

    private const string TemporaryIndexPrefix = ".index-";
    private const string PendingMessagePrefix = ".pending-";

    private readonly IContentFileSystem fileSystem;

    /// <summary>
    /// Creates the mail store's files in <paramref name="stateFolderPath"/>, read and written
    /// through <paramref name="fileSystem"/>. Nothing is read or written until asked.
    /// </summary>
    /// <param name="fileSystem">The seam every read and write goes through.</param>
    /// <param name="stateFolderPath">The full path of the folder the index is kept in.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="stateFolderPath"/> is empty.</exception>
    public MailStoreFiles(IContentFileSystem fileSystem, string stateFolderPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrEmpty(stateFolderPath);

        this.fileSystem = fileSystem;
        StateFolderPath = stateFolderPath;
        IndexPath = Path.Join(stateFolderPath, IndexFileName);
        MessagesFolderPath = Path.Join(stateFolderPath, MessagesFolderName);
    }

    /// <summary>
    /// The full path of the folder the index is kept in.
    /// </summary>
    public string StateFolderPath { get; }

    /// <summary>
    /// The full path of the index: <see cref="IndexFileName"/> in <see cref="StateFolderPath"/>.
    /// </summary>
    public string IndexPath { get; }

    /// <summary>
    /// The full path of the folder the message files are kept in:
    /// <see cref="MessagesFolderName"/> in <see cref="StateFolderPath"/>.
    /// </summary>
    public string MessagesFolderPath { get; }

    /// <summary>
    /// The full path of one message file: its number as 16 lower-case hexadecimal digits in
    /// <see cref="MessagesFolderPath"/>.
    /// </summary>
    /// <param name="fileNumber">The message file number.</param>
    /// <returns>The file's full path.</returns>
    public string MessageFilePath(ulong fileNumber) =>
        Path.Join(MessagesFolderPath, fileNumber.ToString("x16", CultureInfo.InvariantCulture));

    /// <summary>
    /// Reads the index's bytes; <see langword="null"/> when there is no index.
    /// </summary>
    /// <exception cref="MailStoreLoadException">The index cannot be read.</exception>
    internal async Task<byte[]?> ReadIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            return fileSystem.GetEntryKind(IndexPath) == ContentEntryKind.File
                ? await ReadAllAsync(IndexPath, cancellationToken)
                : null;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new MailStoreLoadException(IndexPath, exception.Message, exception);
        }
    }

    /// <summary>
    /// Checks that one message file the index names is there and holds the
    /// <paramref name="size"/> bytes the index gives it. Its bytes are not read: a server reads
    /// them from the file when it fetches the message.
    /// </summary>
    /// <exception cref="MailStoreLoadException">The file is missing, not
    /// <paramref name="size"/> bytes long, or cannot be examined.</exception>
    internal void RequireMessageFile(ulong fileNumber, long size)
    {
        var path = MessageFilePath(fileNumber);
        bool matches;
        try
        {
            matches = fileSystem.GetEntryKind(path) == ContentEntryKind.File && fileSystem.GetFileLength(path) == size;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new MailStoreLoadException(path, exception.Message, exception);
        }

        if (!matches)
        {
            throw new MailStoreLoadException(path, MismatchedMessageFileReason);
        }
    }

    /// <summary>
    /// Opens one message file to read its bytes.
    /// </summary>
    internal Stream OpenMessage(ulong fileNumber) => fileSystem.OpenFileForAsyncRead(MessageFilePath(fileNumber));

    /// <summary>
    /// Creates a new, empty pending file, <c>messages/.pending-&lt;guid&gt;</c>, for a message
    /// body to be streamed into, creating its folder first.
    /// </summary>
    /// <param name="pendingPath">The pending file's full path.</param>
    /// <returns>The stream the body is written through.</returns>
    internal Stream CreatePendingMessage(out string pendingPath)
    {
        fileSystem.CreateDirectory(MessagesFolderPath);
        pendingPath = Path.Join(MessagesFolderPath, PendingMessagePrefix + Guid.NewGuid().ToString("N"));
        return fileSystem.CreateFileForAsyncWrite(pendingPath);
    }

    /// <summary>
    /// Renames a written pending file to its message file number; a file already at the number,
    /// left by a crash, is replaced.
    /// </summary>
    internal void KeepPendingMessage(string pendingPath, ulong fileNumber) =>
        fileSystem.MoveFileReplacing(pendingPath, MessageFilePath(fileNumber));

    /// <summary>
    /// Deletes a pending file; nothing happens when there is none.
    /// </summary>
    internal void DeletePendingMessage(string pendingPath) => fileSystem.DeleteFile(pendingPath);

    /// <summary>
    /// Replaces the index with <paramref name="bytes"/>, through a temporary file renamed into
    /// place.
    /// </summary>
    internal Task WriteIndexAsync(byte[] bytes, CancellationToken cancellationToken) =>
        WriteReplacingAsync(StateFolderPath, TemporaryIndexPrefix, IndexPath, bytes, cancellationToken);

    private async Task WriteReplacingAsync(string folderPath, string temporaryPrefix, string destinationPath, byte[] bytes, CancellationToken cancellationToken)
    {
        fileSystem.CreateDirectory(folderPath);
        var temporaryPath = Path.Join(folderPath, temporaryPrefix + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var stream = fileSystem.CreateFileForAsyncWrite(temporaryPath))
            {
                await stream.WriteAsync(bytes, cancellationToken);
            }

            fileSystem.MoveFileReplacing(temporaryPath, destinationPath);
        }
        catch
        {
            fileSystem.DeleteFile(temporaryPath);
            throw;
        }
    }

    /// <summary>
    /// Deletes one message file; nothing happens when there is none.
    /// </summary>
    internal void DeleteMessage(ulong fileNumber) => fileSystem.DeleteFile(MessageFilePath(fileNumber));

    /// <summary>
    /// Runs <paramref name="action"/>, and catches the exceptions a file system throws when a
    /// file cannot be read or written: <see cref="IOException"/> and
    /// <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    /// <returns>The caught exception's message; <see langword="null"/> when none was thrown.</returns>
    internal static string? CatchStorageFailure(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return exception.Message;
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/>, and catches what <see cref="CatchStorageFailure"/> catches.
    /// </summary>
    /// <returns>The caught exception's message; <see langword="null"/> when none was thrown.</returns>
    internal static async Task<string?> CatchStorageFailureAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return exception.Message;
        }
    }

    private static bool IsStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    private async Task<byte[]> ReadAllAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = new MemoryStream();
        await using (var stream = fileSystem.OpenFileForAsyncRead(path))
        {
            await stream.CopyToAsync(bytes, cancellationToken);
        }

        return bytes.ToArray();
    }
}
