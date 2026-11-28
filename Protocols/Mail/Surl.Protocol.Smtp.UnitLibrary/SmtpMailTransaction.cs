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
    private readonly List<(string Path, MailRecipient Recipient)> resolved = [];

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
    /// <see cref="Deliverable"/> split into one recipient per owner, in its order and then each element's owner
    /// order. Each records, in <see cref="MailRecipient.DeliveredFor"/>, only the paths that resolved to its owner,
    /// as sent and in <c>RCPT</c> order; each of the anonymous owner's records its own path alone. So no owner's
    /// recipient names a path that reached only other owners. With <c>--mail-delivered-to</c> each of these recipients
    /// has its own pending message, whose <c>Delivered-To</c> lines name those paths alone (ADR-0053 Amendment 1,
    /// decisions 3, 4 and 7).
    /// </summary>
    /// <returns>The recipients to deliver to, each of one owner.</returns>
    public IReadOnlyList<MailRecipient> DeliverablePerOwner() =>
        [.. Deliverable.SelectMany(recipient => recipient.OwnerNames.Select(ownerName => OneOwnerOf(recipient, ownerName)))];

    private MailRecipient OneOwnerOf(MailRecipient recipient, string ownerName)
    {
        var paths = resolved
            .Where(entry => ownerName.Length == 0
                ? ReferenceEquals(entry.Recipient, recipient)
                : entry.Recipient.OwnerNames.Contains(ownerName))
            .Select(entry => entry.Path);
        // The one owner kept is not among those taken away, so something remains.
        return recipient.WithoutOwners(recipient.OwnerNames.Where(other => other != ownerName))!.ForAddresses(paths);
    }

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
        if (recipient is not null)
        {
            resolved.Add((path, recipient));
        }

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
