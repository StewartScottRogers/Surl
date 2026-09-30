using System.Text;

namespace Surl.MailStore;

/// <summary>
/// Builds stores and delivers mail for the store's tests.
/// </summary>
internal static class MailStoreFixture
{
    public static MailboxStore NewStore(params string[] accountNames) =>
        new(accountNames, allowAnonymous: false, new SettableTimeProvider());

    public static MailRecipient Recipient(MailboxStore store, string path)
    {
        Assert.AreEqual(MailRecipientLookup.Deliverable, store.LookUpRecipient(path, out var recipient));
        return recipient!;
    }

    public static MailStoreOutcome Deliver(MailboxStore store, string message, params string[] paths) =>
        store.Deliver([.. paths.Select(path => Recipient(store, path))], Encoding.ASCII.GetBytes(message));

    public static string Fetch(MailboxStore store, MailView view, string mailboxName, uint uid)
    {
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.FetchMessage(view, mailboxName, uid, out var message));
        return Encoding.ASCII.GetString(message.Span);
    }

    public static MailboxSnapshot Read(MailboxStore store, MailView view, string mailboxName)
    {
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ReadMailbox(view, mailboxName, out var snapshot));
        return snapshot!;
    }

    public static uint[] Uids(MailboxStore store, MailView view, string mailboxName) =>
        [.. Read(store, view, mailboxName).Messages.Select(message => message.Uid)];
}
