using Surl.Content;

namespace Surl.MailStore;

/// <summary>
/// Loads persisted stores over an in-memory file system, and reads and writes its files, for
/// the persistence tests.
/// </summary>
internal static class PersistedStoreFixture
{
    public static readonly string StateFolder = Path.Join(InMemoryContentFileSystem.RootPath, ".surl", "mail");

    public static readonly string IndexPath = Path.Join(StateFolder, "index");

    public static readonly string MessagesFolder = Path.Join(StateFolder, "messages");

    public static InMemoryContentFileSystem NewFileSystem() => new(new SettableTimeProvider());

    public static string MessagePath(ulong fileNumber) => Path.Join(MessagesFolder, fileNumber.ToString("x16", System.Globalization.CultureInfo.InvariantCulture));

    public static Task<MailboxStore> LoadAsync(
        IContentFileSystem fileSystem,
        string[] accountNames,
        bool allowAnonymous = false,
        TimeProvider? timeProvider = null,
        int maxMessages = MailboxStore.DefaultMaxMessages,
        long maxTotalMessageBytes = MailboxStore.DefaultMaxTotalMessageBytes,
        int maxMailboxes = MailboxStore.DefaultMaxMailboxes) =>
        MailboxStore.LoadAsync(
            new MailStoreFiles(fileSystem, StateFolder),
            accountNames,
            allowAnonymous,
            timeProvider ?? new SettableTimeProvider(),
            maxMessages: maxMessages,
            maxTotalMessageBytes: maxTotalMessageBytes,
            maxMailboxes: maxMailboxes);

    public static void WriteFile(InMemoryContentFileSystem fileSystem, string path, byte[] bytes)
    {
        fileSystem.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = fileSystem.CreateFileForAsyncWrite(path);
        stream.Write(bytes);
    }

    public static byte[] ReadFile(IContentFileSystem fileSystem, string path)
    {
        using var stream = fileSystem.OpenFileForAsyncRead(path);
        var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    public static bool Exists(IContentFileSystem fileSystem, string path) =>
        fileSystem.GetEntryKind(path) == ContentEntryKind.File;
}
