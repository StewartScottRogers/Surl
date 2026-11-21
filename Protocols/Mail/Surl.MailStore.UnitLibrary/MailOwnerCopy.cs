namespace Surl.MailStore;

/// <summary>
/// One recipient's own copy of a message, as
/// <see cref="MailboxStore.Deliver(IReadOnlyList{MailOwnerCopy}, Surl.Protocol.Abstractions.ExchangeContext)"/>
/// takes it: the owners it goes to, its own pending message, and the envelope addresses that
/// reached those owners (ADR-0053 Amendment 1, decisions 4 and 7).
/// </summary>
public sealed class MailOwnerCopy
{
    /// <summary>
    /// Makes one owner's copy.
    /// </summary>
    /// <param name="recipient">The recipient whose owners get this copy; an owner of a recipient with several owners shares the copy's message file.</param>
    /// <param name="message">The copy's own pending message, its bytes all written.</param>
    /// <param name="deliveredFor">The envelope addresses, as sent and in <c>RCPT</c> order, that resolved to the owners; <see langword="null"/> for those <paramref name="recipient"/> records, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="recipient"/> or <paramref name="message"/> is <see langword="null"/>.</exception>
    public MailOwnerCopy(MailRecipient recipient, PendingMessage message, IReadOnlyList<string>? deliveredFor = null)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(message);
        Recipient = recipient;
        Message = message;
        DeliveredFor = deliveredFor ?? recipient.DeliveredFor;
    }

    /// <summary>
    /// The recipient whose owners get this copy.
    /// </summary>
    public MailRecipient Recipient { get; }

    /// <summary>
    /// The copy's own pending message.
    /// </summary>
    public PendingMessage Message { get; }

    /// <summary>
    /// The envelope addresses that reached the owners, or <see langword="null"/> when none are recorded.
    /// </summary>
    public IReadOnlyList<string>? DeliveredFor { get; }
}
