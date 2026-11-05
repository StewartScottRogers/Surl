using System.Text;
using Surl.Content;

namespace Surl.MessageBus;

/// <summary>
/// A data directory's <c>.surl</c> folder in a <see cref="UnitTestFaultingContentFileSystem"/>,
/// and the buses loaded from it, so a test can save a bus, load another from the same files,
/// read the bytes written and plant bytes to load.
/// </summary>
internal sealed class PersistedBusFixture
{
    public static readonly string SurlFolderPath = Path.Join(InMemoryContentFileSystem.RootPath, ".surl");

    public PersistedBusFixture()
    {
        Files = new MessageBusFiles(FileSystem, SurlFolderPath);
    }

    public SettableTimeProvider Clock { get; } = new();

    public UnitTestFaultingContentFileSystem FileSystem { get; } = new();

    public MessageBusFiles Files { get; }

    public static BusMessage Message(string topic, string payload) => new(topic, Encoding.UTF8.GetBytes(payload));

    public static string Text(BusMessage message) => Encoding.UTF8.GetString(message.Payload.Span);

    public static byte[] Bytes(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    public MessageBus NewBus(int maxTopics = MessageBus.DefaultMaxTopics, long maxRetainedPayloadBytes = MessageBus.DefaultMaxRetainedPayloadBytes, int maxQueues = MessageBus.DefaultMaxQueues, int maxMessagesPerQueue = MessageBus.DefaultMaxMessagesPerQueue, long maxQueuedBytes = MessageBus.DefaultMaxQueuedBytes, long maxLogBytesPerTopic = MessageBus.DefaultMaxLogBytesPerTopic, long maxLogBytes = MessageBus.DefaultMaxLogBytes) =>
        new(Clock, maxTopics: maxTopics, maxRetainedPayloadBytes: maxRetainedPayloadBytes, maxQueues: maxQueues, maxMessagesPerQueue: maxMessagesPerQueue, maxQueuedBytes: maxQueuedBytes, maxLogBytesPerTopic: maxLogBytesPerTopic, maxLogBytes: maxLogBytes);

    /// <summary>A new bus with default bounds, loaded from the files.</summary>
    public async Task<MessageBus> LoadBusAsync()
    {
        var bus = NewBus();
        Assert.AreEqual(BusSaveResult.Saved, await bus.LoadAsync(Files));
        return bus;
    }

    public bool Exists(string path) => FileSystem.Files.GetEntryKind(path) == ContentEntryKind.File;

    public byte[] Read(string path)
    {
        using var stream = FileSystem.Files.OpenFileForAsyncRead(path);
        var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    public void Write(string path, byte[] bytes)
    {
        FileSystem.Files.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = FileSystem.Files.CreateFileForAsyncWrite(path);
        stream.Write(bytes);
    }

    public string[] Entries(string folderPath) =>
        FileSystem.Files.GetEntryKind(folderPath) == ContentEntryKind.Directory
            ? [.. FileSystem.Files.EnumerateDirectoryEntryNames(folderPath).Order(StringComparer.Ordinal)]
            : [];

    /// <summary>Loads a new bus with default bounds and returns the load exception it must throw.</summary>
    public async Task<MessageBusLoadException> LoadFailureAsync() => await LoadFailureAsync(NewBus());

    public async Task<MessageBusLoadException> LoadFailureAsync(MessageBus bus) =>
        await Assert.ThrowsExactlyAsync<MessageBusLoadException>(() => bus.LoadAsync(Files));
}
