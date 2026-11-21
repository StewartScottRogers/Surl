using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// An <see cref="IControlTarget"/> that records each call as one line, answers as a test sets it up, and hands out
/// the events a test writes to <see cref="Events"/>.
/// </summary>
internal sealed class FakeControlTarget : IControlTarget
{
    private int snapshotCount;

    /// <summary>Each call, as one line.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>The events <see cref="ReadEventsAsync"/> hands out.</summary>
    public Channel<SurlEvent> Events { get; } = Channel.CreateUnbounded<SurlEvent>();

    /// <summary>What every member but <see cref="ReadEventsAsync"/> throws, or <see langword="null"/>.</summary>
    public Exception? Failure { get; set; }

    /// <summary>What <see cref="ListFaults"/> answers.</summary>
    public List<ControlFault> Faults { get; } = [];

    /// <summary>What the removing members answer.</summary>
    public bool Found { get; set; } = true;

    /// <summary>The snapshots taken, in order.</summary>
    public List<IControlSnapshot> Snapshots { get; } = [];

    /// <summary>The last message published.</summary>
    public ControlMessage? LastMessage { get; private set; }

    public async IAsyncEnumerable<SurlEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var surlEvent in Events.Reader.ReadAllAsync(cancellationToken))
        {
            yield return surlEvent;
        }
    }

    public string AddFault(string item)
    {
        Record($"AddFault {item}");

        return "f7";
    }

    public IReadOnlyList<ControlFault> ListFaults()
    {
        Record("ListFaults");

        return Faults;
    }

    public bool RemoveFault(string faultId)
    {
        Record($"RemoveFault {faultId}");

        return Found;
    }

    /// <summary>What <see cref="IControlTarget.WritesDeliveredTo"/> answers.</summary>
    public bool WritesDeliveredTo { get; set; }

    public Task SeedMailAsync(string recipient, ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        Record($"SeedMail {recipient} {Encoding.UTF8.GetString(message.Span)}");

        return Task.CompletedTask;
    }

    public Task WriteFileAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        Record($"WriteFile {path} {Encoding.UTF8.GetString(content.Span)}");

        return Task.CompletedTask;
    }

    public Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken)
    {
        Record($"DeleteFile {path}");

        return Task.FromResult(Found);
    }

    public Task PublishMessageAsync(ControlMessage message, CancellationToken cancellationToken)
    {
        Record($"PublishMessage {message.Topic ?? "-"} {message.Queue ?? "-"} {Encoding.UTF8.GetString(message.Payload.Span)} {message.Retain}");
        LastMessage = message;

        return Task.CompletedTask;
    }

    public Task SeedKeyAsync(int database, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, TimeSpan? expiry, CancellationToken cancellationToken)
    {
        Record($"SeedKey {database} {Encoding.UTF8.GetString(key.Span)} {Encoding.UTF8.GetString(value.Span)} {expiry?.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}");

        return Task.CompletedTask;
    }

    public Task SeedDirectoryAsync(string ldif, CancellationToken cancellationToken)
    {
        Record($"SeedDirectory {ldif}");

        return Task.CompletedTask;
    }

    public Task SeedZoneAsync(string apex, string zoneFileText, CancellationToken cancellationToken)
    {
        Record($"SeedZone {apex} {zoneFileText}");

        return Task.CompletedTask;
    }

    public Task AddAccountAsync(string user, string password, CancellationToken cancellationToken)
    {
        Record($"AddAccount {user} {password}");

        return Task.CompletedTask;
    }

    public Task<bool> SetAccountPasswordAsync(string user, string password, CancellationToken cancellationToken)
    {
        Record($"SetAccountPassword {user} {password}");

        return Task.FromResult(Found);
    }

    public Task<bool> SetProxyAccountPasswordAsync(string user, string password, CancellationToken cancellationToken)
    {
        Record($"SetProxyAccountPassword {user} {password}");

        return Task.FromResult(Found);
    }

    public Task<bool> SetTlsSrpAccountPasswordAsync(string user, string password, CancellationToken cancellationToken)
    {
        Record($"SetTlsSrpAccountPassword {user} {password}");

        return Task.FromResult(Found);
    }

    public Task<bool> RemoveAccountAsync(string user, CancellationToken cancellationToken)
    {
        Record($"RemoveAccount {user}");

        return Task.FromResult(Found);
    }

    public Task AddProxyAccountAsync(string user, string password, CancellationToken cancellationToken)
    {
        Record($"AddProxyAccount {user} {password}");

        return Task.CompletedTask;
    }

    public Task<bool> RemoveProxyAccountAsync(string user, CancellationToken cancellationToken)
    {
        Record($"RemoveProxyAccount {user}");

        return Task.FromResult(Found);
    }

    public Task AddTlsSrpAccountAsync(string user, string password, CancellationToken cancellationToken)
    {
        Record($"AddTlsSrpAccount {user} {password}");

        return Task.CompletedTask;
    }

    public Task<bool> RemoveTlsSrpAccountAsync(string user, CancellationToken cancellationToken)
    {
        Record($"RemoveTlsSrpAccount {user}");

        return Task.FromResult(Found);
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        Record("Reset");

        return Task.CompletedTask;
    }

    public Task<IControlSnapshot> SnapshotAsync(CancellationToken cancellationToken)
    {
        Record("Snapshot");
        IControlSnapshot snapshot = new FakeSnapshot(++snapshotCount);
        Snapshots.Add(snapshot);

        return Task.FromResult(snapshot);
    }

    public Task RestoreAsync(IControlSnapshot snapshot, CancellationToken cancellationToken)
    {
        Record($"Restore {((FakeSnapshot)snapshot).Number}");

        return Task.CompletedTask;
    }

    /// <summary>What <see cref="ListMailboxesAsync"/> answers.</summary>
    public List<ControlMailbox> Mailboxes { get; } = [];

    /// <summary>What <see cref="ListMessagesAsync"/> answers.</summary>
    public ControlMailMessagePage MessagePage { get; set; } = new([], false);

    /// <summary>What <see cref="ReadMessageAsync"/> answers.</summary>
    public ControlMailMessage? Message { get; set; }

    /// <summary>What <see cref="ReadFileAsync"/> answers.</summary>
    public ControlFileContents? File { get; set; }

    /// <summary>What <see cref="ListFolderAsync"/> answers.</summary>
    public ControlFolderPage FolderPage { get; set; } = new([], false);

    /// <summary>What <see cref="BrowseQueueAsync"/> answers.</summary>
    public ControlQueuePage QueuePage { get; set; } = new([], 0, 0, false);

    /// <summary>What <see cref="ReadRetainedMessageAsync"/> answers.</summary>
    public ControlRetainedMessage? Retained { get; set; }

    /// <summary>What <see cref="ReadLogAsync"/> answers.</summary>
    public ControlLogPage LogPage { get; set; } = new([], 0, false);

    public Task<IReadOnlyList<ControlMailbox>> ListMailboxesAsync(string owner, CancellationToken cancellationToken)
    {
        Record($"ListMailboxes {owner}");

        return Task.FromResult<IReadOnlyList<ControlMailbox>>(Mailboxes);
    }

    public Task<ControlMailMessagePage> ListMessagesAsync(string owner, string mailbox, uint afterUid, int limit, CancellationToken cancellationToken)
    {
        Record($"ListMessages {owner} {mailbox} {afterUid} {limit}");

        return Task.FromResult(MessagePage);
    }

    public Task<ControlMailMessage> ReadMessageAsync(string owner, string mailbox, uint uid, CancellationToken cancellationToken)
    {
        Record($"ReadMessage {owner} {mailbox} {uid}");

        return Task.FromResult(Message!);
    }

    public Task<ControlFileContents> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        Record($"ReadFile {path}");

        return Task.FromResult(File!);
    }

    public Task<ControlFolderPage> ListFolderAsync(string path, string? afterName, int limit, CancellationToken cancellationToken)
    {
        Record($"ListFolder {path} {afterName ?? "-"} {limit}");

        return Task.FromResult(FolderPage);
    }

    public Task<ControlQueuePage> BrowseQueueAsync(string queue, int afterPosition, int limit, CancellationToken cancellationToken)
    {
        Record($"BrowseQueue {queue} {afterPosition} {limit}");

        return Task.FromResult(QueuePage);
    }

    public Task<ControlRetainedMessage> ReadRetainedMessageAsync(string topic, CancellationToken cancellationToken)
    {
        Record($"ReadRetained {topic}");

        return Task.FromResult(Retained!);
    }

    public Task<ControlLogPage> ReadLogAsync(string topic, long fromOffset, int limit, CancellationToken cancellationToken)
    {
        Record($"ReadLog {topic} {fromOffset} {limit}");

        return Task.FromResult(LogPage);
    }

    /// <summary>What <see cref="ReadKeyAsync"/> answers.</summary>
    public ControlKeyContents KeyContents { get; set; } = new(new ControlKeyInfo(new byte[] { 107 }, "string", 1, null), new byte[] { 118 });

    /// <summary>What <see cref="ListKeysAsync"/> answers.</summary>
    public ControlKeyPage KeyPage { get; set; } = new([], false);

    public Task<ControlKeyContents> ReadKeyAsync(int database, ReadOnlyMemory<byte> key, CancellationToken cancellationToken)
    {
        Record($"ReadKey {database} {System.Text.Encoding.UTF8.GetString(key.Span)}");

        return Task.FromResult(KeyContents);
    }

    public Task<ControlKeyPage> ListKeysAsync(int database, string pattern, ReadOnlyMemory<byte> afterKey, int limit, CancellationToken cancellationToken)
    {
        Record($"ListKeys {database} {pattern} {System.Text.Encoding.UTF8.GetString(afterKey.Span)} {limit}");

        return Task.FromResult(KeyPage);
    }

    /// <summary>What <see cref="ReadDirectoryLdifAsync"/> answers.</summary>
    public string DirectoryLdif { get; set; } = "version: 1\n";

    public Task<string> ReadDirectoryLdifAsync(CancellationToken cancellationToken)
    {
        Record("ReadDirectory");

        return Task.FromResult(DirectoryLdif);
    }

    /// <summary>What <see cref="ListZonesAsync"/> answers.</summary>
    public IReadOnlyList<string> ZoneApexes { get; set; } = [];

    /// <summary>What <see cref="ReadZoneAsync"/> answers.</summary>
    public string ZoneText { get; set; } = "example.test. 60 IN SOA a. b. 1 2 3 4 5\n";

    public Task<IReadOnlyList<string>> ListZonesAsync(CancellationToken cancellationToken)
    {
        Record("ListZones");

        return Task.FromResult(ZoneApexes);
    }

    public Task<string> ReadZoneAsync(string apex, CancellationToken cancellationToken)
    {
        Record($"ReadZone {apex}");

        return Task.FromResult(ZoneText);
    }

    private void Record(string call)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Calls.Add(call);
    }

    /// <summary>A snapshot the fake took, numbered from 1.</summary>
    /// <param name="Number">Its number.</param>
    public sealed record FakeSnapshot(int Number) : IControlSnapshot;
}
