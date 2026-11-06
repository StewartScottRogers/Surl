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
    /// The UIDs the session sees, in ascending order: message <c>n</c> is <c>Uids[n - 1]</c>.
    /// </summary>
    public IReadOnlyList<uint> Uids => uids;

    /// <summary>
    /// The highest UID the session sees, what <c>*</c> stands for in a UID set; 0 when it sees
    /// no message.
    /// </summary>
    public uint HighestUid => uids.Count == 0 ? 0 : uids[^1];

    /// <summary>
    /// The messages a <c>FETCH</c> or <c>UID FETCH</c> names (ADR-0055, decision 4), in ascending
    /// order.
    /// </summary>
    /// <param name="set">The sequence set.</param>
    /// <param name="isUidSet">Whether the set holds UIDs: those that name no message are left
    /// out. Otherwise it holds message numbers, all of which must name a message.</param>
    /// <returns>Each message's number and UID, or <see langword="null"/> when a message number
    /// names no message.</returns>
    public IReadOnlyList<(int Number, uint Uid)>? Resolve(ImapSequenceSet set, bool isUidSet)
    {
        var count = (uint)uids.Count;
        if (!isUidSet && !set.IsWithin(count))
        {
            return null;
        }

        return uids.Select((uid, index) => (Number: index + 1, Uid: uid))
            .Where(message => isUidSet ? set.Contains(message.Uid, HighestUid) : set.Contains((uint)message.Number, count))
            .ToList();
    }

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
    public IReadOnlyList<string> Update(MailboxSnapshot? snapshot) => snapshot is null ? RemoveExpunged(_ => false) : Refresh(snapshot);

    /// <summary>
    /// Takes the messages this session itself removed out of the view, as <c>MOVE</c> does
    /// (RFC 6851): <c>* &lt;n&gt; EXPUNGE</c> for each, highest number first, and no other update.
    /// </summary>
    /// <param name="removedUids">The UIDs removed; one the view does not hold is ignored.</param>
    /// <returns>The untagged lines to send.</returns>
    public IReadOnlyList<string> Remove(IReadOnlyCollection<uint> removedUids)
    {
        var removed = removedUids.ToHashSet();
        return RemoveExpunged(uid => !removed.Contains(uid));
    }

    private List<string> Refresh(MailboxSnapshot snapshot)
    {
        var present = snapshot.Messages.Select(message => message.Uid).ToHashSet();
        var lines = RemoveExpunged(present.Contains);
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
    private List<string> RemoveExpunged(Func<uint, bool> isKept)
    {
        List<string> lines = [];
        for (var index = uids.Count - 1; index >= 0; index--)
        {
            if (!isKept(uids[index]))
            {
                lines.Add($"* {index + 1} EXPUNGE");
                uids.RemoveAt(index);
            }
        }

        return lines;
    }
}
