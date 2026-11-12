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
    /// The owners to deliver to: an account named twice once, an alias whose owners were all
    /// named already not again, the anonymous owner once per recipient (ADR-0053, decision 4;
    /// ADR-0102, decision 1.3).
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
        else if (recipient.OwnerName.Length == 0)
        {
            deliverable.Add(recipient);
        }
        else if (!recipient.OwnerNames.All(deliverableOwners.Contains))
        {
            KeepNamedRecipient(recipient);
        }
    }

    // One copy per owner per message (ADR-0102, decision 1.3): a kept recipient whose owners the
    // new one names all of is replaced by it, and the new one then keeps only the owners no
    // kept recipient reaches, so two partly overlapping aliases give the shared owner one copy.
    private void KeepNamedRecipient(MailRecipient recipient)
    {
        var owners = recipient.OwnerNames;
        deliverable.RemoveAll(kept => kept.OwnerNames.All(owners.Contains));
        deliverableOwners.Clear();
        deliverableOwners.UnionWith(deliverable.SelectMany(kept => kept.OwnerNames));
        // AddRecipient only calls this when an owner is unreached, and removing kept recipients
        // only shrinks the reached set, so something remains.
        var unreached = recipient.WithoutOwners(deliverableOwners)!;
        deliverable.Add(unreached);
        deliverableOwners.UnionWith(unreached.OwnerNames);
    }
}
