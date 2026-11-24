using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// An <see cref="IControlTarget"/> written before <see cref="IControlTarget.WritesDeliveredTo"/> existed: it hands every
/// member to a <see cref="FakeControlTarget"/> and does not implement that one, so the interface's default answers.
/// </summary>
internal sealed class TargetWrittenBeforeDeliveredToControlTarget(FakeControlTarget inner) : IControlTarget
{
    public IAsyncEnumerable<SurlEvent> ReadEventsAsync(CancellationToken cancellationToken) => inner.ReadEventsAsync(cancellationToken);

    public string AddFault(string item) => inner.AddFault(item);

    public IReadOnlyList<ControlFault> ListFaults() => inner.ListFaults();

    public bool RemoveFault(string faultId) => inner.RemoveFault(faultId);

    public Task SeedMailAsync(string recipient, ReadOnlyMemory<byte> message, CancellationToken cancellationToken) => inner.SeedMailAsync(recipient, message, cancellationToken);

    public Task WriteFileAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) => inner.WriteFileAsync(path, content, cancellationToken);

    public Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken) => inner.DeleteFileAsync(path, cancellationToken);

    public Task PublishMessageAsync(ControlMessage message, CancellationToken cancellationToken) => inner.PublishMessageAsync(message, cancellationToken);

    public Task SeedKeyAsync(int database, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, TimeSpan? expiry, CancellationToken cancellationToken) => inner.SeedKeyAsync(database, key, value, expiry, cancellationToken);

    public Task SeedDirectoryAsync(string ldif, CancellationToken cancellationToken) => inner.SeedDirectoryAsync(ldif, cancellationToken);

    public Task SeedZoneAsync(string apex, string zoneFileText, CancellationToken cancellationToken) => inner.SeedZoneAsync(apex, zoneFileText, cancellationToken);

    public Task AddAccountAsync(string user, string password, CancellationToken cancellationToken) => inner.AddAccountAsync(user, password, cancellationToken);

    public Task<bool> SetAccountPasswordAsync(string user, string password, CancellationToken cancellationToken) => inner.SetAccountPasswordAsync(user, password, cancellationToken);

    public Task<bool> SetProxyAccountPasswordAsync(string user, string password, CancellationToken cancellationToken) => inner.SetProxyAccountPasswordAsync(user, password, cancellationToken);

    public Task<bool> SetTlsSrpAccountPasswordAsync(string user, string password, CancellationToken cancellationToken) => inner.SetTlsSrpAccountPasswordAsync(user, password, cancellationToken);

    public Task<bool> RemoveAccountAsync(string user, CancellationToken cancellationToken) => inner.RemoveAccountAsync(user, cancellationToken);

    public Task AddProxyAccountAsync(string user, string password, CancellationToken cancellationToken) => inner.AddProxyAccountAsync(user, password, cancellationToken);

    public Task<bool> RemoveProxyAccountAsync(string user, CancellationToken cancellationToken) => inner.RemoveProxyAccountAsync(user, cancellationToken);

    public Task AddTlsSrpAccountAsync(string user, string password, CancellationToken cancellationToken) => inner.AddTlsSrpAccountAsync(user, password, cancellationToken);

    public Task<bool> RemoveTlsSrpAccountAsync(string user, CancellationToken cancellationToken) => inner.RemoveTlsSrpAccountAsync(user, cancellationToken);

    public Task ResetAsync(CancellationToken cancellationToken) => inner.ResetAsync(cancellationToken);

    public Task<IControlSnapshot> SnapshotAsync(CancellationToken cancellationToken) => inner.SnapshotAsync(cancellationToken);

    public Task RestoreAsync(IControlSnapshot snapshot, CancellationToken cancellationToken) => inner.RestoreAsync(snapshot, cancellationToken);

    public Task<IReadOnlyList<ControlMailbox>> ListMailboxesAsync(string owner, CancellationToken cancellationToken) => inner.ListMailboxesAsync(owner, cancellationToken);

    public Task<ControlMailMessagePage> ListMessagesAsync(string owner, string mailbox, uint afterUid, int limit, CancellationToken cancellationToken) => inner.ListMessagesAsync(owner, mailbox, afterUid, limit, cancellationToken);

    public Task<ControlMailMessage> ReadMessageAsync(string owner, string mailbox, uint uid, CancellationToken cancellationToken) => inner.ReadMessageAsync(owner, mailbox, uid, cancellationToken);

    public Task<ControlFileContents> ReadFileAsync(string path, CancellationToken cancellationToken) => inner.ReadFileAsync(path, cancellationToken);

    public Task<ControlFolderPage> ListFolderAsync(string path, string? afterName, int limit, CancellationToken cancellationToken) => inner.ListFolderAsync(path, afterName, limit, cancellationToken);

    public Task<ControlQueuePage> BrowseQueueAsync(string queue, int afterPosition, int limit, CancellationToken cancellationToken) => inner.BrowseQueueAsync(queue, afterPosition, limit, cancellationToken);

    public Task<ControlRetainedMessage> ReadRetainedMessageAsync(string topic, CancellationToken cancellationToken) => inner.ReadRetainedMessageAsync(topic, cancellationToken);

    public Task<ControlLogPage> ReadLogAsync(string topic, long fromOffset, int limit, CancellationToken cancellationToken) => inner.ReadLogAsync(topic, fromOffset, limit, cancellationToken);

    public Task<string> ReadDirectoryLdifAsync(CancellationToken cancellationToken) => inner.ReadDirectoryLdifAsync(cancellationToken);

    public Task<IReadOnlyList<string>> ListZonesAsync(CancellationToken cancellationToken) => inner.ListZonesAsync(cancellationToken);

    public Task<string> ReadZoneAsync(string apex, CancellationToken cancellationToken) => inner.ReadZoneAsync(apex, cancellationToken);

    public Task<ControlKeyContents> ReadKeyAsync(int database, ReadOnlyMemory<byte> key, CancellationToken cancellationToken) => inner.ReadKeyAsync(database, key, cancellationToken);

    public Task<ControlKeyPage> ListKeysAsync(int database, string pattern, ReadOnlyMemory<byte> afterKey, int limit, CancellationToken cancellationToken) => inner.ListKeysAsync(database, pattern, afterKey, limit, cancellationToken);
}
