using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// What the loopback control endpoint drives (ADR-0124 decision 7): the instance's event stream, its fault plan and
/// its stores. <c>Surl.Composition</c> implements it over a composed instance (BL-816); the endpoint never sees a
/// protocol server.
/// </summary>
/// <remarks>
/// A member refuses a request it cannot carry out by throwing <see cref="ControlRefusalException"/>, whose message
/// is sent to the client and so never holds a token or a password. Every change a member makes is published on the
/// instance's event stream by the store or fault plan it changes, not by the endpoint.
/// </remarks>
public interface IControlTarget
{
    /// <summary>
    /// Reads the instance's events, bytes included, from a subscription made when the read starts, until
    /// <paramref name="cancellationToken"/> is cancelled. The endpoint's own exchanges are left out.
    /// </summary>
    /// <param name="cancellationToken">Ends the subscription.</param>
    /// <returns>The events, in the stream's order.</returns>
    IAsyncEnumerable<SurlEvent> ReadEventsAsync(CancellationToken cancellationToken);

    /// <summary>Adds a fault rule given as one <c>--fault</c> item, refusing a bad item with the reader's text.</summary>
    /// <param name="item">The item, as <c>--fault</c> takes it.</param>
    /// <returns>The rule's ID, such as <c>f3</c>.</returns>
    string AddFault(string item);

    /// <summary>Lists the fault plan's rules in the order they are tried.</summary>
    /// <returns>The rules.</returns>
    IReadOnlyList<ControlFault> ListFaults();

    /// <summary>Removes a fault rule.</summary>
    /// <param name="faultId">The rule's ID.</param>
    /// <returns>Whether a rule had that ID.</returns>
    bool RemoveFault(string faultId);

    /// <summary>
    /// Whether the instance writes <c>Delivered-To</c> lines into stored mail (<c>--mail-delivered-to</c>, ADR-0053
    /// amendment 1 decision 6). When true the endpoint puts one <c>Delivered-To: &lt;recipient&gt;</c> line ahead of a
    /// message it seeds. False unless a target says otherwise, so a target written before the option is unchanged.
    /// </summary>
    bool WritesDeliveredTo => false;
    /// <summary>Delivers a message to a mail recipient as a seed.</summary>
    /// <param name="recipient">The recipient address.</param>
    /// <param name="message">The message, as RFC 5322 bytes.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>The delivery.</returns>
    Task SeedMailAsync(string recipient, ReadOnlyMemory<byte> message, CancellationToken cancellationToken);

    /// <summary>Writes a file into the content store as a seed, replacing any file at its path.</summary>
    /// <param name="path">The file's path, starting with <c>/</c>.</param>
    /// <param name="content">The file's bytes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The write.</returns>
    Task WriteFileAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>Deletes a file from the content store.</summary>
    /// <param name="path">The file's path, starting with <c>/</c>.</param>
    /// <param name="cancellationToken">Cancels the deletion.</param>
    /// <returns>Whether a file was at that path.</returns>
    Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken);

    /// <summary>Publishes or enqueues a message on the message bus as a seed.</summary>
    /// <param name="message">The message and where it goes.</param>
    /// <param name="cancellationToken">Cancels the publication.</param>
    /// <returns>The publication.</returns>
    Task PublishMessageAsync(ControlMessage message, CancellationToken cancellationToken);

    /// <summary>Sets a string key in the Redis keyspace as a seed (ADR-0158 decision 9).</summary>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="key">The key, at least 1 byte.</param>
    /// <param name="value">The value's bytes.</param>
    /// <param name="expiry">How long the key lives, or <see langword="null"/> for no expiry.</param>
    /// <param name="cancellationToken">Cancels the seed.</param>
    /// <returns>The seed.</returns>
    Task SeedKeyAsync(int database, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, TimeSpan? expiry, CancellationToken cancellationToken);

    /// <summary>Adds LDIF content records to the LDAP directory as a seed, all or none.</summary>
    /// <param name="ldif">The LDIF content records.</param>
    /// <param name="cancellationToken">Cancels the seed.</param>
    /// <returns>The seed.</returns>
    Task SeedDirectoryAsync(string ldif, CancellationToken cancellationToken);

    /// <summary>Adds one DNS zone as a seed.</summary>
    /// <param name="apex">The zone's apex.</param>
    /// <param name="zoneFileText">The zone file's text.</param>
    /// <param name="cancellationToken">Cancels the seed.</param>
    /// <returns>The seed.</returns>
    Task SeedZoneAsync(string apex, string zoneFileText, CancellationToken cancellationToken);

    /// <summary>Adds an account (ADR-0132 amendment 1).</summary>
    /// <param name="user">The account name.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">Cancels the addition.</param>
    /// <returns>The addition; refused <see cref="ControlRefusalKind.Conflict"/> when an account has that name, which is kept.</returns>
    Task AddAccountAsync(string user, string password, CancellationToken cancellationToken);

    /// <summary>Gives an account a new password, keeping its other members (ADR-0132 amendment 1).</summary>
    /// <param name="user">The account name.</param>
    /// <param name="password">The new password.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <returns>Whether an account had that name; when none did, nothing changed.</returns>
    Task<bool> SetAccountPasswordAsync(string user, string password, CancellationToken cancellationToken);

    /// <summary>Gives a proxy account a new password (ADR-0132 amendment 1).</summary>
    /// <param name="user">The account name.</param>
    /// <param name="password">The new password.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <returns>Whether a proxy account had that name; refused <see cref="ControlRefusalKind.Conflict"/> when none is configured.</returns>
    Task<bool> SetProxyAccountPasswordAsync(string user, string password, CancellationToken cancellationToken);

    /// <summary>Gives a TLS-SRP account a new password (ADR-0132 amendment 1).</summary>
    /// <param name="user">The account name.</param>
    /// <param name="password">The new password.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <returns>Whether a TLS-SRP account had that name; refused <see cref="ControlRefusalKind.Conflict"/> when TLS-SRP is not configured.</returns>
    Task<bool> SetTlsSrpAccountPasswordAsync(string user, string password, CancellationToken cancellationToken);

    /// <summary>Removes an account.</summary>
    /// <param name="user">The account name.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>Whether an account had that name.</returns>
    Task<bool> RemoveAccountAsync(string user, CancellationToken cancellationToken);

    /// <summary>Adds a proxy account (ADR-0170 decision 4; a name already taken is refused as for accounts).</summary>
    /// <param name="user">The account name.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">Cancels the addition.</param>
    /// <returns>
    /// The addition; refused <see cref="ControlRefusalKind.Conflict"/> when a proxy account has that name, which is
    /// kept, or when no proxy account is configured.
    /// </returns>
    Task AddProxyAccountAsync(string user, string password, CancellationToken cancellationToken);

    /// <summary>Removes a proxy account.</summary>
    /// <param name="user">The account name.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>Whether a proxy account had that name; refused <see cref="ControlRefusalKind.Conflict"/> when none is configured.</returns>
    Task<bool> RemoveProxyAccountAsync(string user, CancellationToken cancellationToken);

    /// <summary>Adds a TLS-SRP account (ADR-0170 decision 4; a name already taken is refused as for accounts).</summary>
    /// <param name="user">The account name.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">Cancels the addition.</param>
    /// <returns>
    /// The addition; refused <see cref="ControlRefusalKind.Conflict"/> when a TLS-SRP account has that name, which is
    /// kept, or when TLS-SRP is not configured.
    /// </returns>
    Task AddTlsSrpAccountAsync(string user, string password, CancellationToken cancellationToken);

    /// <summary>Removes a TLS-SRP account.</summary>
    /// <param name="user">The account name.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>Whether a TLS-SRP account had that name; refused <see cref="ControlRefusalKind.Conflict"/> when TLS-SRP is not configured.</returns>
    Task<bool> RemoveTlsSrpAccountAsync(string user, CancellationToken cancellationToken);

    /// <summary>Returns every store to its configured state (ADR-0124 decision 3).</summary>
    /// <param name="cancellationToken">Cancels the reset.</param>
    /// <returns>The reset.</returns>
    Task ResetAsync(CancellationToken cancellationToken);

    /// <summary>Takes a snapshot of every store.</summary>
    /// <param name="cancellationToken">Cancels the snapshot.</param>
    /// <returns>The snapshot.</returns>
    Task<IControlSnapshot> SnapshotAsync(CancellationToken cancellationToken);

    /// <summary>Restores every store from a snapshot this target took.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="cancellationToken">Cancels the restore.</param>
    /// <returns>The restore.</returns>
    Task RestoreAsync(IControlSnapshot snapshot, CancellationToken cancellationToken);

    // The read-back members (ADR-0164 decisions 1 to 4). None publishes an event or consults the fault plan; each
    // refuses by throwing ControlRefusalException: NotFound for an absent item, TooLarge for one past 16 MiB and
    // BadRequest for a bound or path a request may not name.

    /// <summary>Lists an owner's mailboxes.</summary>
    /// <param name="owner">The account name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The mailboxes.</returns>
    Task<IReadOnlyList<ControlMailbox>> ListMailboxesAsync(string owner, CancellationToken cancellationToken);

    /// <summary>Lists a page of one mailbox's messages in UID order.</summary>
    /// <param name="owner">The account name.</param>
    /// <param name="mailbox">The mailbox's name.</param>
    /// <param name="afterUid">The last UID seen, or 0.</param>
    /// <param name="limit">The most messages, 1 to 1000.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page.</returns>
    Task<ControlMailMessagePage> ListMessagesAsync(string owner, string mailbox, uint afterUid, int limit, CancellationToken cancellationToken);

    /// <summary>Reads one message by UID without setting any flag.</summary>
    /// <param name="owner">The account name.</param>
    /// <param name="mailbox">The mailbox's name.</param>
    /// <param name="uid">The UID.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The message.</returns>
    Task<ControlMailMessage> ReadMessageAsync(string owner, string mailbox, uint uid, CancellationToken cancellationToken);

    /// <summary>Reads one file of the content store.</summary>
    /// <param name="path">The file's path, starting with <c>/</c>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file.</returns>
    Task<ControlFileContents> ReadFileAsync(string path, CancellationToken cancellationToken);

    /// <summary>Lists a page of one content-store folder's children.</summary>
    /// <param name="path">The folder's path, starting with <c>/</c>.</param>
    /// <param name="afterName">The last name seen, or <see langword="null"/>.</param>
    /// <param name="limit">The most children, 1 to 1000.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page.</returns>
    Task<ControlFolderPage> ListFolderAsync(string path, string? afterName, int limit, CancellationToken cancellationToken);

    /// <summary>Browses a queue's ready messages without consuming or acknowledging any.</summary>
    /// <param name="queue">The queue's name.</param>
    /// <param name="afterPosition">The last position seen, or -1.</param>
    /// <param name="limit">The most messages, 1 to 1000.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page.</returns>
    Task<ControlQueuePage> BrowseQueueAsync(string queue, int afterPosition, int limit, CancellationToken cancellationToken);

    /// <summary>Reads a topic's retained message.</summary>
    /// <param name="topic">The topic.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The retained message.</returns>
    Task<ControlRetainedMessage> ReadRetainedMessageAsync(string topic, CancellationToken cancellationToken);

    /// <summary>Reads a page of a topic log from an offset.</summary>
    /// <param name="topic">The topic.</param>
    /// <param name="fromOffset">The first offset to read.</param>
    /// <param name="limit">The most records, 1 to 1000.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page.</returns>
    Task<ControlLogPage> ReadLogAsync(string topic, long fromOffset, int limit, CancellationToken cancellationToken);

    /// <summary>Reads the LDAP directory as LDIF, <c>userPassword</c> values omitted (ADR-0164 decision 9).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The LDIF text.</returns>
    Task<string> ReadDirectoryLdifAsync(CancellationToken cancellationToken);

    /// <summary>Lists the apexes of the DNS zones held, each with its final dot (ADR-0164 decision 9).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The apexes, in the order the zones were added.</returns>
    Task<IReadOnlyList<string>> ListZonesAsync(CancellationToken cancellationToken);

    /// <summary>Reads one DNS zone as master-file text (ADR-0164 decision 9).</summary>
    /// <param name="apex">The zone's apex, with or without its final dot.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The zone-file text.</returns>
    Task<string> ReadZoneAsync(string apex, CancellationToken cancellationToken);

    /// <summary>Reads one live Redis key without removing an expired one (ADR-0164 decision 9).</summary>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="key">The key's bytes.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The key; a string's value is included, another type's is not.</returns>
    Task<ControlKeyContents> ReadKeyAsync(int database, ReadOnlyMemory<byte> key, CancellationToken cancellationToken);

    /// <summary>Lists a page of live Redis keys matching a <c>KEYS</c> glob in ordinal order, without values.</summary>
    /// <param name="database">The database, 0 to 15.</param>
    /// <param name="pattern">The glob.</param>
    /// <param name="afterKey">The last key seen, or empty.</param>
    /// <param name="limit">The most keys, 1 to 1000.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page.</returns>
    Task<ControlKeyPage> ListKeysAsync(int database, string pattern, ReadOnlyMemory<byte> afterKey, int limit, CancellationToken cancellationToken);
}
