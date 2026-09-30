using System.Globalization;
using Surl.MailStore;

namespace Surl.Protocol.Imap;

/// <summary>
/// The mailbox a session selected and the session's view of it (ADR-0055, decision 5): the
/// UIDs it has numbered 1 upward, which change only when the session is told of another
/// session's expunges and additions.
/// </summary>
internal sealed class ImapSelectedMailbox
{
    private readonly List<uint> uids;
    private uint nextUid;

    private ImapSelectedMailbox(MailboxSnapshot snapshot, bool isReadOnly)
    {
        Name = snapshot.Name;
        IsReadOnly = isReadOnly;
        uids = [.. snapshot.Messages.Select(message => message.Uid)];
        nextUid = snapshot.NextUid;
    }

    /// <summary>
    /// The mailbox's name, as the store keeps it.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether it was opened with <c>EXAMINE</c>.
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    /// Selects the mailbox <paramref name="snapshot"/> shows, and writes the untagged data of
    /// <c>SELECT</c> or, when <paramref name="isReadOnly"/>, <c>EXAMINE</c>.
    /// </summary>
    /// <param name="snapshot">The mailbox as it is now.</param>
    /// <param name="isReadOnly">Whether it is opened with <c>EXAMINE</c>.</param>
    /// <param name="lines">The untagged lines, in decision 5's order.</param>
    /// <returns>The session's view of it.</returns>
    public static ImapSelectedMailbox Open(MailboxSnapshot snapshot, bool isReadOnly, out IReadOnlyList<string> lines)
    {
        var firstUnseen = snapshot.Messages.ToList().FindIndex(message => !message.Flags.HasFlag(MailFlags.Seen));
        List<string> untagged =
        [
            "* FLAGS " + ImapResponses.SystemFlags,
            isReadOnly ? "* OK [PERMANENTFLAGS ()] No permanent flags permitted" : $"* OK [PERMANENTFLAGS {ImapResponses.SystemFlags}] Flags permitted",
            $"* {snapshot.Messages.Count} EXISTS",
            "* 0 RECENT",
        ];
        if (firstUnseen >= 0)
        {
            untagged.Add($"* OK [UNSEEN {firstUnseen + 1}] First unseen");
        }

        untagged.Add($"* OK [UIDVALIDITY {snapshot.UidValidity.ToString(CultureInfo.InvariantCulture)}] UIDs valid");
        untagged.Add($"* OK [UIDNEXT {snapshot.NextUid.ToString(CultureInfo.InvariantCulture)}] Predicted next UID");
        lines = untagged;
        return new ImapSelectedMailbox(snapshot, isReadOnly);
    }

    /// <summary>
    /// Brings the view up to <paramref name="snapshot"/>: <c>* &lt;n&gt; EXPUNGE</c> for each
    /// message gone, highest number first, then <c>* &lt;n&gt; EXISTS</c> when messages were added.
    /// </summary>
    /// <param name="snapshot">The mailbox as it is now, or <see langword="null"/> when it no
    /// longer exists, which is as if every message were expunged.</param>
    /// <returns>The untagged lines to send before the next tagged response.</returns>
    public IReadOnlyList<string> Update(MailboxSnapshot? snapshot) => snapshot is null ? RemoveExpunged([]) : Refresh(snapshot);

    private List<string> Refresh(MailboxSnapshot snapshot)
    {
        var lines = RemoveExpunged(snapshot.Messages.Select(message => message.Uid).ToHashSet());
        var added = snapshot.Messages.Where(message => message.Uid >= nextUid).Select(message => message.Uid).ToList();
        nextUid = snapshot.NextUid;
        uids.AddRange(added);
        if (added.Count > 0)
        {
            lines.Add($"* {uids.Count} EXISTS");
        }

        return lines;
    }

    // Numbers the gone messages from the highest down, so each number is right when it is read.
    private List<string> RemoveExpunged(HashSet<uint> present)
    {
        List<string> lines = [];
        for (var index = uids.Count - 1; index >= 0; index--)
        {
            if (!present.Contains(uids[index]))
            {
                lines.Add($"* {index + 1} EXPUNGE");
                uids.RemoveAt(index);
            }
        }

        return lines;
    }
}
