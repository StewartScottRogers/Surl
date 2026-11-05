using System.Globalization;
using Surl.Content;

namespace Surl.MessageBus;

/// <summary>
/// The files that keep a <see cref="MessageBus"/> across restarts, read and written only
/// through <see cref="IContentFileSystem"/> (ADR-0100, decision 3.7, with ADR-0031 decision 6's
/// and ADR-0050 decision 7's rules).
/// </summary>
/// <remarks>
/// <para>
/// <b>Where.</b> Under <see cref="StateFolderPath"/>, <c>&lt;data directory&gt;/.surl/bus</c>:
/// <see cref="RetainedFileName"/>; <c>queues/index</c> with each durable queue message in its own
/// file in <c>queues/messages</c>; and one file per topic log in <c>logs</c>. Message and log
/// files are named by their number as 16 lower-case hexadecimal digits. Folders are created,
/// with every missing folder above them, before each write.
/// </para>
/// <para>
/// <b>Bytes.</b> Every integer is big-endian; a string or byte field is its 4-byte length then
/// its bytes, UTF-8 for a string; a message is <see cref="BusMessageCodec"/>'s form, every
/// property kept. <c>retained</c>: <see cref="RetainedHeader"/>, then each retained message in
/// ordinal order of topic. <c>queues/index</c>: <see cref="QueueIndexHeader"/>, the queue count
/// (4 bytes), and per queue in ordinal order of name: its name, its filter count (4 bytes) and
/// filters in ordinal order, its message count (4 bytes), and per message in queue order its
/// file number (8 bytes) and a redelivered byte (0 or 1). A queue message file:
/// <see cref="QueueMessageHeader"/> and the message. A log file: <see cref="LogHeader"/>, the
/// topic, the log start offset (8 bytes), then each held record's message in offset order.
/// </para>
/// <para>
/// <b>How it is written.</b> <c>retained</c>, <c>queues/index</c> and each queue message file
/// to <c>.&lt;name&gt;-&lt;guid&gt;</c> beside it, then renamed over it, so a failed write
/// leaves the file as it was; a queue message file is written once and deleted when its
/// message leaves every durable queue. A log file is appended to while its log only grew, and
/// written whole, the same way, when its start offset rose or a write to it failed.
/// </para>
/// <para>
/// <b>How it is read.</b> Once, at load. A missing file is empty, a file whose name is not
/// one of these is ignored, and a malformed one is a <see cref="MessageBusLoadException"/> with
/// <see cref="MalformedFileReason"/>.
/// </para>
/// </remarks>
public sealed class MessageBusFiles
{
    /// <summary>The bus's state folder's name in <c>.surl</c>: <c>bus</c>.</summary>
    public const string StateFolderName = "bus";

    /// <summary>The retained messages' file name: <c>retained</c>.</summary>
    public const string RetainedFileName = "retained";

    /// <summary>The durable queues' folder name: <c>queues</c>.</summary>
    public const string QueuesFolderName = "queues";

    /// <summary>The durable queues' index name in <see cref="QueuesFolderName"/>: <c>index</c>.</summary>
    public const string QueueIndexFileName = "index";

    /// <summary>The queue message files' folder name in <see cref="QueuesFolderName"/>: <c>messages</c>.</summary>
    public const string QueueMessagesFolderName = "messages";

    /// <summary>The topic logs' folder name: <c>logs</c>.</summary>
    public const string LogsFolderName = "logs";

    /// <summary>The reason a bus file that does not parse, or holds more than the bus's bounds
    /// allow, is refused with: <c>not a message bus file</c>.</summary>
    public const string MalformedFileReason = "not a message bus file";

    /// <summary>The reason a queue message file the index names is refused with when it is not
    /// there: <c>missing, though the queue index names it</c>.</summary>
    public const string MissingQueueMessageFileReason = "missing, though the queue index names it";

    /// <summary>The reason an old MQTT 3.1.1 <c>retained-messages</c> file that does not parse is
    /// refused with, as before (ADR-0031 decision 6): <c>not a retained-message file</c>.</summary>
    public const string MalformedMqttRetainedFileReason = "not a retained-message file";

    private readonly IContentFileSystem fileSystem;

    /// <summary>
    /// Creates the bus's files under <paramref name="surlFolderPath"/>, read and written through
    /// <paramref name="fileSystem"/>. Nothing is read or written until asked.
    /// </summary>
    /// <param name="fileSystem">The seam every read and write goes through.</param>
    /// <param name="surlFolderPath">The full path of the data directory's <c>.surl</c> folder.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="surlFolderPath"/> is empty.</exception>
    public MessageBusFiles(IContentFileSystem fileSystem, string surlFolderPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrEmpty(surlFolderPath);

        this.fileSystem = fileSystem;
        StateFolderPath = Path.Join(surlFolderPath, StateFolderName);
        RetainedPath = Path.Join(StateFolderPath, RetainedFileName);
        QueuesFolderPath = Path.Join(StateFolderPath, QueuesFolderName);
        QueueIndexPath = Path.Join(QueuesFolderPath, QueueIndexFileName);
        QueueMessagesFolderPath = Path.Join(QueuesFolderPath, QueueMessagesFolderName);
        LogsFolderPath = Path.Join(StateFolderPath, LogsFolderName);
        MqttRetainedMessagesPath = Path.Join(surlFolderPath, "mqtt", "retained-messages");
    }

    /// <summary>The 20 ASCII bytes <c>retained</c> starts with: <c>SURL-BUS-RETAINED-1</c> and a line feed.</summary>
    public static ReadOnlySpan<byte> RetainedHeader => "SURL-BUS-RETAINED-1\n"u8;

    /// <summary>The 18 ASCII bytes <c>queues/index</c> starts with: <c>SURL-BUS-QUEUES-1</c> and a line feed.</summary>
    public static ReadOnlySpan<byte> QueueIndexHeader => "SURL-BUS-QUEUES-1\n"u8;

    /// <summary>The 19 ASCII bytes a queue message file starts with: <c>SURL-BUS-MESSAGE-1</c> and a line feed.</summary>
    public static ReadOnlySpan<byte> QueueMessageHeader => "SURL-BUS-MESSAGE-1\n"u8;

    /// <summary>The 15 ASCII bytes a log file starts with: <c>SURL-BUS-LOG-1</c> and a line feed.</summary>
    public static ReadOnlySpan<byte> LogHeader => "SURL-BUS-LOG-1\n"u8;

    /// <summary>The full path of the bus's state folder, <c>.surl/bus</c>.</summary>
    public string StateFolderPath { get; }

    /// <summary>The full path of <see cref="RetainedFileName"/>.</summary>
    public string RetainedPath { get; }

    /// <summary>The full path of the durable queues' folder.</summary>
    public string QueuesFolderPath { get; }

    /// <summary>The full path of the durable queues' index.</summary>
    public string QueueIndexPath { get; }

    /// <summary>The full path of the queue message files' folder.</summary>
    public string QueueMessagesFolderPath { get; }

    /// <summary>The full path of the topic logs' folder.</summary>
    public string LogsFolderPath { get; }

    /// <summary>The full path of the MQTT 3.1.1 server's old <c>.surl/mqtt/retained-messages</c>
    /// (ADR-0031 decision 6), read at load when <see cref="RetainedPath"/> is not there and never
    /// written (ADR-0100, decision 3.5).</summary>
    public string MqttRetainedMessagesPath { get; }

    /// <summary>The full path of one queue message file.</summary>
    /// <param name="fileNumber">The file's number.</param>
    /// <returns>Its number as 16 lower-case hexadecimal digits in <see cref="QueueMessagesFolderPath"/>.</returns>
    public string QueueMessageFilePath(ulong fileNumber) => Path.Join(QueueMessagesFolderPath, NumberName(fileNumber));

    /// <summary>The full path of one topic's log file.</summary>
    /// <param name="fileNumber">The file's number.</param>
    /// <returns>Its number as 16 lower-case hexadecimal digits in <see cref="LogsFolderPath"/>.</returns>
    public string LogFilePath(ulong fileNumber) => Path.Join(LogsFolderPath, NumberName(fileNumber));

    internal static byte[] EncodeRetained(IEnumerable<BusMessage> messages)
    {
        var writer = new BusFileWriter(RetainedHeader);
        foreach (var message in messages.OrderBy(message => message.Topic, StringComparer.Ordinal))
        {
            BusMessageCodec.Write(writer, message);
        }

        return writer.ToArray();
    }

    internal static byte[] EncodeQueueIndex(IReadOnlyCollection<QueueIndexEntry> queues)
    {
        var writer = new BusFileWriter(QueueIndexHeader);
        writer.WriteCount(queues.Count);
        foreach (var queue in queues.OrderBy(queue => queue.Name, StringComparer.Ordinal))
        {
            writer.WriteString(queue.Name);
            writer.WriteCount(queue.Filters.Count);
            foreach (var filter in queue.Filters.Order(StringComparer.Ordinal))
            {
                writer.WriteString(filter);
            }

            writer.WriteCount(queue.Messages.Count);
            foreach (var (fileNumber, redelivered) in queue.Messages)
            {
                writer.WriteUInt64(fileNumber);
                writer.WriteByte(redelivered ? (byte)1 : (byte)0);
            }
        }

        return writer.ToArray();
    }

    internal static byte[] EncodeQueueMessage(BusMessage message)
    {
        var writer = new BusFileWriter(QueueMessageHeader);
        BusMessageCodec.Write(writer, message);
        return writer.ToArray();
    }

    internal static byte[] EncodeLog(string topic, long startOffset, IEnumerable<BusMessage> records)
    {
        var writer = new BusFileWriter(LogHeader);
        writer.WriteString(topic);
        writer.WriteUInt64((ulong)startOffset);
        return [.. writer.ToArray(), .. EncodeLogRecords(records)];
    }

    internal static byte[] EncodeLogRecords(IEnumerable<BusMessage> records)
    {
        var writer = new BusFileWriter();
        foreach (var record in records)
        {
            BusMessageCodec.Write(writer, record);
        }

        return writer.ToArray();
    }

    /// <summary>Reads every file the bus keeps.</summary>
    /// <exception cref="MessageBusLoadException">A file cannot be read or does not parse.</exception>
    internal async Task<MessageBusFileContents> ReadAsync(CancellationToken cancellationToken)
    {
        var (retained, source, migrated) = await ReadRetainedAsync(cancellationToken);
        var queues = await ReadQueuesAsync(cancellationToken);
        var logs = await ReadLogsAsync(cancellationToken);
        return new(retained, source, migrated, queues, logs);
    }

    /// <summary>Writes <paramref name="bytes"/> to a temporary file in <paramref name="folderPath"/>
    /// and renames it over <paramref name="path"/>; the temporary file is deleted when that fails.</summary>
    internal async Task WriteReplacingAsync(string folderPath, string path, byte[] bytes, CancellationToken cancellationToken)
    {
        fileSystem.CreateDirectory(folderPath);
        var temporaryPath = Path.Join(folderPath, "." + Path.GetFileName(path) + "-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var stream = fileSystem.CreateFileForAsyncWrite(temporaryPath))
            {
                await stream.WriteAsync(bytes, cancellationToken);
            }

            fileSystem.MoveFileReplacing(temporaryPath, path);
        }
        catch
        {
            fileSystem.DeleteFile(temporaryPath);
            throw;
        }
    }

    /// <summary>Appends <paramref name="bytes"/> to the existing file at <paramref name="path"/>.</summary>
    internal async Task AppendAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = fileSystem.OpenFileForAsyncReadWrite(path);
        stream.Seek(0, SeekOrigin.End);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    internal void DeleteFile(string path) => fileSystem.DeleteFile(path);

    /// <summary>Runs <paramref name="write"/>; returns the message of the storage failure it threw,
    /// or <see langword="null"/>.</summary>
    internal static async Task<string?> CatchStorageFailureAsync(Func<Task> write)
    {
        try
        {
            await write();
            return null;
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return exception.Message;
        }
    }

    private static bool IsStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    private static string NumberName(ulong number) => number.ToString("x16", CultureInfo.InvariantCulture);

    private static ulong? ParseNumberName(string name) =>
        ulong.TryParse(name, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var number) && NumberName(number) == name
            ? number
            : null;

    private static T Decode<T>(string path, Func<T> decode)
    {
        try
        {
            return decode();
        }
        catch (InvalidDataException exception)
        {
            throw new MessageBusLoadException(path, exception.Message, exception);
        }
    }

    private static List<BusMessage> DecodeRetained(byte[] bytes)
    {
        var reader = new BusFileReader(bytes, RetainedHeader, MalformedFileReason);
        var messages = new List<BusMessage>();
        var topics = new HashSet<string>(StringComparer.Ordinal);
        while (!reader.AtEnd)
        {
            var message = BusMessageCodec.Read(reader);
            if (!CanonicalTopicName.IsValidTopic(message.Topic) || !message.Retain || message.Payload.IsEmpty || !topics.Add(message.Topic))
            {
                throw reader.Malformed();
            }

            messages.Add(message);
        }

        return messages;
    }

    private static List<BusMessage> DecodeMqttRetained(byte[] bytes)
    {
        var reader = new BusFileReader(bytes, "SURL-MQTT-RETAINED-1\n"u8, MalformedMqttRetainedFileReason);
        var messages = new List<BusMessage>();
        var topics = new HashSet<string>(StringComparer.Ordinal);
        while (!reader.AtEnd)
        {
            var topic = reader.ReadShortString();
            var payload = reader.ReadBytes();
            if (!CanonicalTopicName.IsValidTopic(topic) || payload.Length == 0 || !topics.Add(topic))
            {
                throw reader.Malformed();
            }

            messages.Add(new BusMessage(topic, payload) { Retain = true, QualityOfService = BusQualityOfService.AtMostOnce });
        }

        return messages;
    }

    private static List<QueueIndexEntry> DecodeQueueIndex(byte[] bytes)
    {
        var reader = new BusFileReader(bytes, QueueIndexHeader, MalformedFileReason);
        var count = reader.ReadCount();
        var queues = new List<QueueIndexEntry>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var fileNumbers = new HashSet<ulong>();
        for (var index = 0; index < count; index++)
        {
            var queue = DecodeQueueIndexEntry(reader, fileNumbers);
            if (!names.Add(queue.Name))
            {
                throw reader.Malformed();
            }

            queues.Add(queue);
        }

        return reader.AtEnd ? queues : throw reader.Malformed();
    }

    private static QueueIndexEntry DecodeQueueIndexEntry(BusFileReader reader, HashSet<ulong> fileNumbers)
    {
        var name = reader.ReadString();
        var filters = new string[reader.ReadCount()];
        for (var index = 0; index < filters.Length; index++)
        {
            filters[index] = reader.ReadString();
        }

        var messages = new (ulong FileNumber, bool Redelivered)[reader.ReadCount()];
        for (var index = 0; index < messages.Length; index++)
        {
            var fileNumber = reader.ReadUInt64();
            var redelivered = reader.ReadByte();
            messages[index] = fileNumbers.Add(fileNumber) && redelivered <= 1 ? (fileNumber, redelivered == 1) : throw reader.Malformed();
        }

        return name.Length > 0 && filters.All(CanonicalTopicName.IsValidFilter) ? new(name, filters, messages) : throw reader.Malformed();
    }

    private static BusMessage DecodeQueueMessage(byte[] bytes)
    {
        var reader = new BusFileReader(bytes, QueueMessageHeader, MalformedFileReason);
        var message = BusMessageCodec.Read(reader);
        return reader.AtEnd ? message : throw reader.Malformed();
    }

    private static (string Topic, long StartOffset, List<BusMessage> Records) DecodeLog(byte[] bytes)
    {
        var reader = new BusFileReader(bytes, LogHeader, MalformedFileReason);
        var topic = reader.ReadString();
        var startOffset = reader.ReadNonNegativeInt64();
        var records = new List<BusMessage>();
        while (!reader.AtEnd)
        {
            var record = BusMessageCodec.Read(reader);
            records.Add(record.Topic == topic ? record : throw reader.Malformed());
        }

        return CanonicalTopicName.IsValidTopic(topic) ? (topic, startOffset, records) : throw reader.Malformed();
    }

    private async Task<(List<BusMessage> Retained, PersistedFileSource Source, bool Migrated)> ReadRetainedAsync(CancellationToken cancellationToken)
    {
        if (await ReadIfPresentAsync(RetainedPath, cancellationToken) is { } bytes)
        {
            return (Decode(RetainedPath, () => DecodeRetained(bytes)), new(RetainedPath, MalformedFileReason), false);
        }

        var source = new PersistedFileSource(MqttRetainedMessagesPath, MalformedMqttRetainedFileReason);
        return await ReadIfPresentAsync(MqttRetainedMessagesPath, cancellationToken) is { } mqttBytes
            ? (Decode(MqttRetainedMessagesPath, () => DecodeMqttRetained(mqttBytes)), source, true)
            : ([], source, false);
    }

    private async Task<List<PersistedQueue>> ReadQueuesAsync(CancellationToken cancellationToken)
    {
        if (await ReadIfPresentAsync(QueueIndexPath, cancellationToken) is not { } indexBytes)
        {
            return [];
        }

        var queues = new List<PersistedQueue>();
        foreach (var entry in Decode(QueueIndexPath, () => DecodeQueueIndex(indexBytes)))
        {
            var messages = new List<PersistedQueueMessage>();
            foreach (var (fileNumber, redelivered) in entry.Messages)
            {
                messages.Add(new(fileNumber, redelivered, await ReadQueueMessageAsync(fileNumber, cancellationToken)));
            }

            queues.Add(new(entry.Name, entry.Filters, messages));
        }

        return queues;
    }

    private async Task<BusMessage> ReadQueueMessageAsync(ulong fileNumber, CancellationToken cancellationToken)
    {
        var path = QueueMessageFilePath(fileNumber);
        var bytes = await ReadIfPresentAsync(path, cancellationToken)
            ?? throw new MessageBusLoadException(path, MissingQueueMessageFileReason);
        return Decode(path, () => DecodeQueueMessage(bytes));
    }

    private async Task<List<PersistedLog>> ReadLogsAsync(CancellationToken cancellationToken)
    {
        var logs = new List<PersistedLog>();
        var topics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fileNumber in ListLogFileNumbers())
        {
            var path = LogFilePath(fileNumber);
            var bytes = (await ReadIfPresentAsync(path, cancellationToken))!;
            var (topic, startOffset, records) = Decode(path, () => DecodeLog(bytes));
            logs.Add(topics.Add(topic)
                ? new(fileNumber, path, topic, startOffset, records)
                : throw new MessageBusLoadException(path, MalformedFileReason));
        }

        return logs;
    }

    private List<ulong> ListLogFileNumbers()
    {
        try
        {
            return fileSystem.GetEntryKind(LogsFolderPath) != ContentEntryKind.Directory
                ? []
                : [.. fileSystem.EnumerateDirectoryEntryNames(LogsFolderPath)
                    .Select(ParseNumberName)
                    .OfType<ulong>()
                    .Where(number => fileSystem.GetEntryKind(LogFilePath(number)) == ContentEntryKind.File)
                    .Order()];
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new MessageBusLoadException(LogsFolderPath, exception.Message, exception);
        }
    }

    private async Task<byte[]?> ReadIfPresentAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (fileSystem.GetEntryKind(path) != ContentEntryKind.File)
            {
                return null;
            }

            var bytes = new MemoryStream();
            await using (var stream = fileSystem.OpenFileForAsyncRead(path))
            {
                await stream.CopyToAsync(bytes, cancellationToken);
            }

            return bytes.ToArray();
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            throw new MessageBusLoadException(path, exception.Message, exception);
        }
    }
}
