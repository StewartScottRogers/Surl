using System.Text;
using Surl.Composition;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// A running <see cref="SurlServer"/>'s mail store, as a test seeds it (ADR-0124, decision 3): the one store
/// <c>smtp</c> delivers into and <c>imap</c> and <c>pop3</c> read. Thread-safe.
/// </summary>
public sealed class SurlServerMail
{
    private readonly ComposedStores stores;

    private readonly Action throwIfStopped;

    internal SurlServerMail(ComposedStores stores, Action throwIfStopped)
    {
        this.stores = stores;
        this.throwIfStopped = throwIfStopped;
    }

    /// <summary>
    /// Delivers <paramref name="message"/> to <paramref name="address"/> as SMTP would after <c>RCPT TO:</c>, so
    /// aliases, quotas and events apply; with <c>--mail-delivered-to</c> on, one <c>Delivered-To: &lt;address&gt;</c> line precedes the message (nothing else is added); the fault plan is not consulted. Publishes a
    /// <see cref="StoreChangedEvent"/> with <see cref="StoreChange.Seeded"/>.
    /// </summary>
    /// <param name="address">The recipient, such as <c>alice@example.com</c>.</param>
    /// <param name="message">The message as SMTP <c>DATA</c> carries it, headers first.</param>
    /// <param name="cancellationToken">Cancels saving a mail store on disk.</param>
    /// <returns>A task that completes once the message is in the store.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    /// <exception cref="SurlSeedRefusedException">The recipient is unknown or invalid (<c>NoSuchAccount</c>,
    /// <c>InvalidAddress</c>, <c>RelayingDenied</c>), or the delivery was refused (<c>StoreFull</c> and the like).</exception>
    public async Task DeliverAsync(string address, ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        var result = await stores.SeedMailAsync(address, stores.MailDeliveredToEnabled ? WithDeliveredTo(address, message) : message, cancellationToken);
        if (result.Recipient != MailRecipientLookup.Deliverable)
        {
            throw new SurlSeedRefusedException(StoreKind.Mail, result.Recipient.ToString(), $"Mail for {address}");
        }

        if (result.Delivery != MailStoreOutcome.Succeeded)
        {
            throw new SurlSeedRefusedException(StoreKind.Mail, $"{result.Delivery}", $"Mail for {address}");
        }
    }

    /// <summary>
    /// Lists the mailboxes of the account <paramref name="owner"/> with their message and unseen counts, in
    /// ordinal order of name. Publishes nothing, asks no fault rule, changes no flag, and works on a
    /// <c>DataDirectory</c> server (ADR-0164, decisions 1 to 3).
    /// </summary>
    /// <param name="owner">The account name, such as <c>bob</c>.</param>
    /// <param name="cancellationToken">Not used: the read does not wait.</param>
    /// <returns>The mailboxes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    /// <exception cref="SurlNotFoundException">The account owns no mailbox.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<IReadOnlyList<SurlMailboxInfo>> ListMailboxesAsync(string owner, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        throwIfStopped();
        return SurlReadResults.Completed<IReadOnlyList<SurlMailboxInfo>>(() =>
            [.. SurlReadResults.ValueOrThrow(stores.ListMailboxes(owner))
                .Select(box => new SurlMailboxInfo(box.Name, box.MessageCount, box.UnseenCount, box.UidValidity, box.Use))]);
    }

    /// <summary>
    /// Lists one mailbox's messages (UID, size, flags, internal date) in UID order, one page at a time. Publishes
    /// nothing, asks no fault rule and sets no flag (ADR-0164, decisions 1 to 3).
    /// </summary>
    /// <param name="owner">The account name.</param>
    /// <param name="mailbox">The mailbox's name; <c>INBOX</c> in any case.</param>
    /// <param name="afterUid">The last UID seen; the page starts above it. 0 starts at the first.</param>
    /// <param name="limit">The most messages to return, 1 to 1000.</param>
    /// <param name="cancellationToken">Not used: the read does not wait.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="mailbox"/> is null.</exception>
    /// <exception cref="SurlNotFoundException">The owner or the mailbox is unknown.</exception>
    /// <exception cref="SurlReadRefusedException">The limit is outside 1 to 1000, or <paramref name="afterUid"/> is not a UID.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<SurlMessagePage> ListMessagesAsync(string owner, string mailbox, long afterUid = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(mailbox);
        throwIfStopped();
        return SurlReadResults.Completed(() =>
        {
            var page = SurlReadResults.ValueOrThrow(stores.ListMessages(owner, mailbox, SurlReadResults.UidOrThrow(afterUid, nameof(afterUid)), limit));
            return new SurlMessagePage([.. page.Messages.Select(Summarize)], page.HasMore);
        });
    }

    /// <summary>
    /// Reads one message's exact bytes with its metadata. Publishes nothing, asks no fault rule and never sets
    /// <c>\Seen</c> (ADR-0164, decisions 1 to 3).
    /// </summary>
    /// <param name="owner">The account name.</param>
    /// <param name="mailbox">The mailbox's name; <c>INBOX</c> in any case.</param>
    /// <param name="uid">The message's UID.</param>
    /// <param name="cancellationToken">Not used: the read does not wait.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="mailbox"/> is null.</exception>
    /// <exception cref="SurlNotFoundException">The owner, mailbox or UID is unknown.</exception>
    /// <exception cref="SurlReadRefusedException">The message is past 16 MiB, or <paramref name="uid"/> is not a UID.</exception>
    /// <exception cref="ObjectDisposedException">The server was stopped.</exception>
    public Task<SurlMailMessage> ReadMessageAsync(string owner, string mailbox, long uid, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(mailbox);
        throwIfStopped();
        return SurlReadResults.Completed(() =>
        {
            var message = SurlReadResults.ValueOrThrow(stores.ReadMessage(owner, mailbox, SurlReadResults.UidOrThrow(uid, nameof(uid))));
            return new SurlMailMessage(Summarize(message.Summary), message.Bytes);
        });
    }

    private static byte[] WithDeliveredTo(string address, ReadOnlyMemory<byte> message)
    {
        var line = Encoding.UTF8.GetBytes($"Delivered-To: {address}\r\n");
        var stamped = new byte[line.Length + message.Length];
        line.CopyTo(stamped, 0);
        message.CopyTo(stamped.AsMemory(line.Length));
        return stamped;
    }

    private static SurlMessageSummary Summarize(ComposedMailMessageSummary summary) =>
        new(summary.Uid, summary.Size, FlagNames(summary.Flags), summary.InternalDate);

    private static List<string> FlagNames(MailFlags flags)
    {
        (MailFlags Flag, string Name)[] known =
            [(MailFlags.Seen, "\\Seen"), (MailFlags.Answered, "\\Answered"), (MailFlags.Flagged, "\\Flagged"), (MailFlags.Deleted, "\\Deleted"), (MailFlags.Draft, "\\Draft")];
        return [.. known.Where(entry => flags.HasFlag(entry.Flag)).Select(entry => entry.Name)];
    }
}
