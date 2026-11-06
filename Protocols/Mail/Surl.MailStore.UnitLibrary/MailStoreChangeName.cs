using System.Globalization;

namespace Surl.MailStore;

/// <summary>
/// The <c>StoreChangedEvent.Name</c> and <c>FaultAppliedEvent.Target</c> the mail store publishes
/// (ADR-0124 decision 1; ADR-0127): <c>&lt;owner&gt;/&lt;mailbox&gt;</c> for a mailbox, and
/// <c>&lt;owner&gt;/&lt;mailbox&gt;/;UID=&lt;uid&gt;</c> for one message in it, the shape of an
/// RFC 5092 IMAP URL's path. The anonymous owner's name is empty, so its <c>INBOX</c> is
/// <c>/INBOX</c>; a shared folder's owner is <see cref="MailboxStore.SharedFolderOwnerName"/>.
/// </summary>
public static class MailStoreChangeName
{
    /// <summary>
    /// The name of one mailbox.
    /// </summary>
    /// <param name="ownerName">The mailbox's owner.</param>
    /// <param name="mailboxName">The mailbox's name as the store holds it, <c>INBOX</c> in capitals.</param>
    /// <returns><c>&lt;owner&gt;/&lt;mailbox&gt;</c>.</returns>
    public static string ForMailbox(string ownerName, string mailboxName) => ownerName + "/" + mailboxName;

    /// <summary>
    /// The name of one message.
    /// </summary>
    /// <param name="ownerName">The mailbox's owner.</param>
    /// <param name="mailboxName">The mailbox's name as the store holds it, <c>INBOX</c> in capitals.</param>
    /// <param name="uid">The message's UID in the mailbox.</param>
    /// <returns><c>&lt;owner&gt;/&lt;mailbox&gt;/;UID=&lt;uid&gt;</c>.</returns>
    public static string ForMessage(string ownerName, string mailboxName, uint uid) =>
        ForMailbox(ownerName, mailboxName) + "/;UID=" + uid.ToString(CultureInfo.InvariantCulture);
}
