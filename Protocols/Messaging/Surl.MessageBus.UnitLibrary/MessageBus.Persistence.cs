namespace Surl.MessageBus;

/// <summary>
/// The bus's persistence under <c>&lt;path&gt;/.surl/bus</c> (ADR-0100, decision 3.7): the
/// retained messages, the durable queues and every topic log are loaded once at start from
/// <see cref="MessageBusFiles"/> and written back by <see cref="SaveChangesAsync"/>. Without
/// files nothing touches the disk.
/// </summary>
public sealed partial class MessageBus
{
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private readonly HashSet<ulong> knownQueueMessageFiles = [];
    private MessageBusFiles? files;
    private long retainedVersion;
    private long savedRetainedVersion;
    private long queuesVersion;
    private long savedQueuesVersion;
    private ulong nextLogFileNumber;
    private ulong nextQueueMessageFileNumber;

    /// <summary>
    /// Loads what <paramref name="busFiles"/> hold into this empty bus and keeps them, so
    /// <see cref="SaveChangesAsync"/> writes every later change to them. Called once, after the
    /// data-directory lock and before any listener binds. When the bus's own <c>retained</c> file
    /// is not there, an old MQTT 3.1.1 <c>.surl/mqtt/retained-messages</c> is read instead: each
    /// record becomes a retained message, QoS 0, with no headers and no expiry, published now, and
    /// the bus's <c>retained</c> file is written at once; the old file is left untouched
    /// (ADR-0100, decision 3.5). A retained topic with no log gets an empty one. A declared
    /// queue the files hold keeps its messages; one they hold with other flags - persisted as
    /// durable by an earlier run - is made again empty (ADR-0102, decision 3.2).
    /// </summary>
    /// <param name="busFiles">The bus's files.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>What writing the files the load changed did: <see cref="BusSaveResult.Saved"/>,
    /// or <see cref="MessageBusOutcome.StorageFailed"/> with the reason, which the caller notes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="busFiles"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The bus was loaded before, or already holds
    /// more than its declarations, each still empty.</exception>
    /// <exception cref="MessageBusLoadException">A file cannot be read, does not parse, or holds
    /// more than the bus's bounds allow; the bus takes nothing from the files.</exception>
    public async Task<BusSaveResult> LoadAsync(MessageBusFiles busFiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(busFiles);
        lock (stateLock)
        {
            if (files is not null || !HoldsOnlyDeclarations())
            {
                throw new InvalidOperationException("A message bus is loaded once, before it holds anything.");
            }
        }

        var contents = await busFiles.ReadAsync(cancellationToken);
        RequireWithinBounds(busFiles, contents);
        lock (stateLock)
        {
            RestoreLogs(contents.Logs);
            RestoreRetained(contents);
            RestoreQueues(contents.Queues);
            DeclareConfiguredTopicsAndQueues();
            files = busFiles;
        }

        return await SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Writes every change made since the last save to the bus's files: <c>retained</c> rewritten
    /// whole, each changed log appended to or rewritten, each new durable queue message's file
    /// and then <c>queues/index</c>, and last the files of messages no durable queue holds any
    /// more deleted. Does nothing for a bus that was never loaded. Saves are serialised, and each
    /// writes the bus as it is when the save starts. A server calls it after each change it makes.
    /// </summary>
    /// <param name="cancellationToken">Cancels the save; the next save writes the change instead.</param>
    /// <returns><see cref="BusSaveResult.Saved"/>, or <see cref="MessageBusOutcome.StorageFailed"/>
    /// with the first failed write's message: the change stays in memory, the next save writes it,
    /// and the caller notes the message with <c>IExchangeLog.Note</c>; nothing ends.</returns>
    public async Task<BusSaveResult> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (files is not { } busFiles)
        {
            return BusSaveResult.Saved;
        }

        await saveLock.WaitAsync(cancellationToken);
        try
        {
            var plan = PlanSave(busFiles);
            var failures = new List<string>();
            await SaveRetainedAsync(busFiles, plan.Retained, failures, cancellationToken);
            foreach (var log in plan.Logs)
            {
                await SaveLogAsync(busFiles, log, failures, cancellationToken);
            }

            await SaveQueuesAsync(busFiles, plan.Queues, failures, cancellationToken);
            return failures.Count == 0 ? BusSaveResult.Saved : new(MessageBusOutcome.StorageFailed, failures[0]);
        }
        finally
        {
            saveLock.Release();
        }
    }

    private void RequireWithinBounds(MessageBusFiles busFiles, MessageBusFileContents contents)
    {
        var logTopics = RequireLogsWithinBounds(busFiles, contents);
        RequireRetainedWithinBounds(contents, logTopics);
        RequireQueuesWithinBounds(busFiles, contents);
    }

    private HashSet<string> RequireLogsWithinBounds(MessageBusFiles busFiles, MessageBusFileContents contents)
    {
        var logTopics = contents.Logs.Select(log => log.Topic).Union(declaredLogTopics).ToHashSet(StringComparer.Ordinal);
        var logRecordBytes = contents.Logs.SelectMany(log => log.Records).Sum(LogRecordBytes);
        Require(logTopics.Count <= MaxTopics && logRecordBytes <= MaxLogBytes, busFiles.LogsFolderPath, MessageBusFiles.MalformedFileReason);
        return logTopics;
    }

    private void RequireRetainedWithinBounds(MessageBusFileContents contents, HashSet<string> logTopics)
    {
        var source = contents.RetainedSource;
        var topicCount = logTopics.Union(contents.Retained.Select(message => message.Topic)).Count();
        var retainedBytes = contents.Retained.Sum(message => (long)message.Payload.Length);
        Require(topicCount <= MaxTopics && retainedBytes <= MaxRetainedPayloadBytes, source.FilePath, source.MalformedReason);
    }

    private void RequireQueuesWithinBounds(MessageBusFiles busFiles, MessageBusFileContents contents)
    {
        var queuedMessages = contents.Queues.SelectMany(queue => queue.Messages).ToList();
        Require(
            contents.Queues.Select(queue => queue.Name).Union(declaredQueueNames).Count() <= MaxQueues
                && contents.Queues.All(queue => queue.Messages.Count <= MaxMessagesPerQueue)
                && queuedMessages.Sum(message => message.Message.PayloadAndNativeFormBytes) <= MaxQueuedBytes,
            busFiles.QueueIndexPath,
            MessageBusFiles.MalformedFileReason);
    }

    private static void Require(bool withinBounds, string filePath, string reason)
    {
        if (!withinBounds)
        {
            throw new MessageBusLoadException(filePath, reason);
        }
    }

    private void RestoreLogs(IReadOnlyList<PersistedLog> persistedLogs)
    {
        foreach (var persisted in persistedLogs)
        {
            var log = new TopicLog { FileNumber = persisted.FileNumber, IsSaved = true, SavedStartOffset = persisted.StartOffset };
            log.StartAt(persisted.StartOffset);
            foreach (var record in persisted.Records)
            {
                log.Append(record, LogRecordBytes(record));
            }

            log.SavedHighWatermark = log.HighWatermark;
            logs[persisted.Topic] = log;
            logBytes += log.Bytes;
            nextLogFileNumber = Math.Max(nextLogFileNumber, persisted.FileNumber + 1);
        }
    }

    private void RestoreRetained(MessageBusFileContents contents)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var message in contents.Retained)
        {
            retainedMessages.Add(message.Topic, contents.RetainedMigrated ? message with { PublishedAt = now } : message);
            retainedPayloadBytes += message.Payload.Length;
            logs.TryAdd(message.Topic, new TopicLog());
        }

        savedRetainedVersion = contents.RetainedMigrated ? retainedVersion - 1 : retainedVersion;
    }

    private void RestoreQueues(IReadOnlyList<PersistedQueue> persistedQueues)
    {
        foreach (var persisted in persistedQueues)
        {
            var queue = new NamedQueue(persisted.Name, new BusQueueFlags(Durable: true), null);
            foreach (var filter in persisted.Filters)
            {
                queue.Bind(filter);
            }

            foreach (var message in persisted.Messages)
            {
                queue.Restore(message.Message, message.Redelivered, message.FileNumber);
                queuedBytes += message.Message.PayloadAndNativeFormBytes;
                knownQueueMessageFiles.Add(message.FileNumber);
                nextQueueMessageFileNumber = Math.Max(nextQueueMessageFileNumber, message.FileNumber + 1);
            }

            queues[persisted.Name] = queue;
        }
    }

    private SavePlan PlanSave(MessageBusFiles busFiles)
    {
        lock (stateLock)
        {
            var retained = retainedVersion == savedRetainedVersion
                ? null
                : new RetainedSave(retainedVersion, MessageBusFiles.EncodeRetained(retainedMessages.Values));
            var logSaves = logs.Select(entry => PlanLogSave(busFiles, entry.Key, entry.Value)).OfType<LogSave>().ToList();
            var queuesSave = queuesVersion == savedQueuesVersion ? null : PlanQueuesSave();
            return new(retained, logSaves, queuesSave);
        }
    }

    private LogSave? PlanLogSave(MessageBusFiles busFiles, string topic, TopicLog log)
    {
        log.FileNumber ??= nextLogFileNumber++;
        var path = busFiles.LogFilePath(log.FileNumber.Value);
        var start = log.LogStartOffset;
        var end = log.HighWatermark;
        if (!log.IsSaved || start != log.SavedStartOffset)
        {
            return new(log, path, Whole: true, MessageBusFiles.EncodeLog(topic, start, log.MessagesFrom(start)), start, end);
        }

        return end == log.SavedHighWatermark
            ? null
            : new(log, path, Whole: false, MessageBusFiles.EncodeLogRecords(log.MessagesFrom(log.SavedHighWatermark)), start, end);
    }

    private QueuesSave PlanQueuesSave()
    {
        var entries = new List<QueueIndexEntry>();
        var newFiles = new List<QueueMessageSave>();
        var referenced = new HashSet<ulong>();
        foreach (var queue in queues.Values.Where(queue => queue.IsPersisted))
        {
            var messages = new List<(ulong FileNumber, bool Redelivered, bool HighPriority)>();
            foreach (var message in queue.AllMessages)
            {
                if (message.FileNumber is not { } fileNumber)
                {
                    fileNumber = nextQueueMessageFileNumber++;
                    newFiles.Add(new(message, fileNumber, MessageBusFiles.EncodeQueueMessage(message.Message)));
                }

                referenced.Add(fileNumber);
                messages.Add((fileNumber, message.HandedOut, message.Message.HighPriority));
            }

            entries.Add(new(queue.Name, [.. queue.Filters], messages));
        }

        return new(queuesVersion, MessageBusFiles.EncodeQueueIndex(entries), newFiles, referenced);
    }

    private async Task SaveRetainedAsync(MessageBusFiles busFiles, RetainedSave? save, List<string> failures, CancellationToken cancellationToken)
    {
        if (save is null)
        {
            return;
        }

        var failure = await MessageBusFiles.CatchStorageFailureAsync(() =>
            busFiles.WriteReplacingAsync(busFiles.StateFolderPath, busFiles.RetainedPath, save.Bytes, cancellationToken));
        if (failure is not null)
        {
            failures.Add(failure);
            return;
        }

        lock (stateLock)
        {
            savedRetainedVersion = save.Version;
        }
    }

    private async Task SaveLogAsync(MessageBusFiles busFiles, LogSave save, List<string> failures, CancellationToken cancellationToken)
    {
        var failure = await MessageBusFiles.CatchStorageFailureAsync(() => save.Whole
            ? busFiles.WriteReplacingAsync(busFiles.LogsFolderPath, save.Path, save.Bytes, cancellationToken)
            : busFiles.AppendAsync(save.Path, save.Bytes, cancellationToken));
        lock (stateLock)
        {
            var log = save.Log;
            log.IsSaved = failure is null;
            log.SavedStartOffset = save.StartOffset;
            log.SavedHighWatermark = save.HighWatermark;
        }

        if (failure is not null)
        {
            failures.Add(failure);
        }
    }

    private async Task SaveQueuesAsync(MessageBusFiles busFiles, QueuesSave? save, List<string> failures, CancellationToken cancellationToken)
    {
        if (save is null)
        {
            return;
        }

        foreach (var newFile in save.NewFiles)
        {
            var path = busFiles.QueueMessageFilePath(newFile.FileNumber);
            var failure = await MessageBusFiles.CatchStorageFailureAsync(() =>
                busFiles.WriteReplacingAsync(busFiles.QueueMessagesFolderPath, path, newFile.Bytes, cancellationToken));
            if (failure is not null)
            {
                failures.Add(failure);
                return;
            }

            newFile.Message.FileNumber = newFile.FileNumber;
            knownQueueMessageFiles.Add(newFile.FileNumber);
        }

        var indexFailure = await MessageBusFiles.CatchStorageFailureAsync(() =>
            busFiles.WriteReplacingAsync(busFiles.QueuesFolderPath, busFiles.QueueIndexPath, save.Index, cancellationToken));
        if (indexFailure is not null)
        {
            failures.Add(indexFailure);
            return;
        }

        lock (stateLock)
        {
            savedQueuesVersion = save.Version;
        }

        await DeleteUnreferencedQueueMessageFilesAsync(busFiles, save.Referenced, failures);
    }

    private async Task DeleteUnreferencedQueueMessageFilesAsync(MessageBusFiles busFiles, HashSet<ulong> referenced, List<string> failures)
    {
        foreach (var fileNumber in knownQueueMessageFiles.Except(referenced).Order().ToList())
        {
            var failure = await MessageBusFiles.CatchStorageFailureAsync(() =>
            {
                busFiles.DeleteFile(busFiles.QueueMessageFilePath(fileNumber));
                return Task.CompletedTask;
            });
            if (failure is null)
            {
                knownQueueMessageFiles.Remove(fileNumber);
            }
            else
            {
                failures.Add(failure);
            }
        }
    }

    private sealed record SavePlan(RetainedSave? Retained, List<LogSave> Logs, QueuesSave? Queues);

    private sealed record RetainedSave(long Version, byte[] Bytes);

    private sealed record LogSave(TopicLog Log, string Path, bool Whole, byte[] Bytes, long StartOffset, long HighWatermark);

    private sealed record QueueMessageSave(QueuedMessage Message, ulong FileNumber, byte[] Bytes);

    private sealed record QueuesSave(long Version, byte[] Index, List<QueueMessageSave> NewFiles, HashSet<ulong> Referenced);
}
