using Surl.MailStore;

namespace Surl.Protocol.Smtp;

/// <summary>
/// One mail transaction, from an accepted <c>MAIL</c> to its <c>DATA</c>, <c>RSET</c>, or the
/// next <c>EHLO</c> or <c>HELO</c> (RFC 5321 section 3.3): the reverse-path and the recipients
/// accepted so far (ADR-0053, decision 4).
/// </summary>
/// <param name="reversePath">The <c>MAIL FROM</c> path between its brackets; empty for <c>&lt;&gt;</c>.</param>
internal sealed class SmtpMailTransaction(string reversePath)
{
    /// <summary>
    /// The most recipients one transaction accepts (RFC 5321 section 4.5.3.1.8).
    /// </summary>
    public const int MaxRecipients = 100;

    private readonly List<MailRecipient> deliverable = [];
    private readonly HashSet<string> deliverableOwners = new(StringComparer.Ordinal);
    private readonly List<string> discarded = [];

    /// <summary>
    /// The <c>MAIL FROM</c> path between its brackets; empty for <c>&lt;&gt;</c>.
    /// </summary>
    public string ReversePath { get; } = reversePath;

    /// <summary>
    /// How many <c>RCPT</c>s were answered <c>250</c>, delivered or discarded.
    /// </summary>
    public int AcceptedRecipientCount { get; private set; }

    /// <summary>
    /// The owners to deliver to: an account named twice once, the anonymous owner once per
    /// recipient (ADR-0053, decision 4).
    /// </summary>
    public IReadOnlyList<MailRecipient> Deliverable => deliverable;

    /// <summary>
    /// The recipients that name no account, whose copies are discarded.
    /// </summary>
    public IReadOnlyList<string> Discarded => discarded;

    /// <summary>
    /// Takes one accepted recipient.
    /// </summary>
    /// <param name="path">The path as sent, for the discard note.</param>
    /// <param name="recipient">Its owner, or <see langword="null"/> when it names no account.</param>
    public void AddRecipient(string path, MailRecipient? recipient)
    {
        AcceptedRecipientCount++;
        if (recipient is null)
        {
            discarded.Add(path);
        }
        else if (recipient.OwnerName.Length == 0 || deliverableOwners.Add(recipient.OwnerName))
        {
            deliverable.Add(recipient);
        }
    }
}
