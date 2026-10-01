using System.Globalization;

namespace Surl.Protocol.Imap;

/// <summary>
/// One data item a <c>FETCH</c> asks for (ADR-0055, decision 4).
/// </summary>
/// <param name="Kind">Which item.</param>
/// <param name="Section">The section of a <see cref="ImapFetchItemKind.BodySection"/> item.</param>
/// <param name="IsPeek">Whether it was asked as <c>BODY.PEEK</c>, which leaves <c>\Seen</c> unset.</param>
/// <param name="PartialOrigin">The first byte of a <c>&lt;origin.length&gt;</c> partial, or
/// <see langword="null"/> for the whole section.</param>
/// <param name="PartialLength">The partial's most bytes.</param>
internal sealed record ImapFetchItem(ImapFetchItemKind Kind, ImapSection? Section = null, bool IsPeek = false, long? PartialOrigin = null, long PartialLength = 0)
{
    /// <summary>
    /// Whether answering it sets <c>\Seen</c> in a read-write mailbox: <c>BODY[...]</c>,
    /// <c>RFC822</c> and <c>RFC822.TEXT</c>, never a <c>.PEEK</c>.
    /// </summary>
    public bool SetsSeen => !IsPeek && Kind is ImapFetchItemKind.BodySection or ImapFetchItemKind.Rfc822 or ImapFetchItemKind.Rfc822Text;

    /// <summary>
    /// Whether answering it reads the message's bytes.
    /// </summary>
    public bool ReadsMessage => Kind is not (ImapFetchItemKind.Uid or ImapFetchItemKind.Flags or ImapFetchItemKind.InternalDate or ImapFetchItemKind.Rfc822Size);

    /// <summary>
    /// The name a <see cref="ImapFetchItemKind.BodySection"/> item is answered under:
    /// <c>BODY[&lt;section&gt;]</c>, then <c>&lt;origin&gt;</c> for a partial; never <c>.PEEK</c>.
    /// </summary>
    public string SectionName =>
        $"BODY[{Section!.Echo}]" + (PartialOrigin is { } origin ? $"<{origin.ToString(CultureInfo.InvariantCulture)}>" : string.Empty);
}
