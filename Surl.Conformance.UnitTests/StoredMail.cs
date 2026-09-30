using Surl.Content;
using Surl.MailStore;

namespace Surl.Conformance;

/// <summary>
/// Reads the mail a stopped <c>surl</c> left in its data directory back through the persisted
/// mail store (ADR-0050, decision 7): <c>&lt;directory&gt;/.surl/mail</c>'s index and message
/// files, loaded by <see cref="MailboxStore.LoadAsync"/> exactly as surl loads them at start.
/// </summary>
internal static class StoredMail
{
    /// <summary>
    /// Returns the bytes of every message in one owner's <c>INBOX</c>, in UID order: the
    /// account <paramref name="accountName"/>'s, or, when it is <see langword="null"/>, the
    /// anonymous owner's that <c>--allow-anonymous</c> delivers to.
    /// </summary>
    public static async Task<IReadOnlyList<byte[]>> ReadInboxAsync(
        string dataDirectory, string? accountName, CancellationToken cancellationToken)
    {
        var files = new MailStoreFiles(new DiskContentFileSystem(), Path.Join(Path.GetFullPath(dataDirectory), ".surl", "mail"));
        var store = await MailboxStore.LoadAsync(
            files,
            accountName is null ? [] : [accountName],
            allowAnonymous: accountName is null,
            TimeProvider.System,
            cancellationToken: cancellationToken);

        var view = store.ViewFor(accountName);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ReadMailbox(view, "INBOX", out var inbox));
        var messages = new List<byte[]>();
        foreach (var summary in inbox!.Messages)
        {
            Assert.AreEqual(MailStoreOutcome.Succeeded, store.FetchMessage(view, "INBOX", summary.Uid, out var message));
            messages.Add(message.ToArray());
        }

        return messages;
    }
}
