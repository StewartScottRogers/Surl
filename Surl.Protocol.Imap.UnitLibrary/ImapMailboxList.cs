namespace Surl.Protocol.Imap;

/// <summary>
/// The untagged data of <c>LIST</c> and <c>LSUB</c> (ADR-0055, decision 6): the names matching
/// the reference and pattern joined, <c>*</c> matching any run of characters and <c>%</c> any
/// run without <c>/</c>; <c>INBOX</c> first, then the rest in ordinal order.
/// </summary>
internal static class ImapMailboxList
{
    /// <summary>
    /// The lines answering <c>LIST</c> or <c>LSUB</c>.
    /// </summary>
    /// <param name="command"><c>LIST</c> or <c>LSUB</c>.</param>
    /// <param name="mailboxNames">The view's mailbox names, as the store lists them.</param>
    /// <param name="pattern">The reference and the pattern, decoded and joined.</param>
    /// <returns>One <c>* LIST</c> or <c>* LSUB</c> line per match. <c>LIST</c> also lists each
    /// parent level that is not a mailbox but has one below it as <c>\Noselect \HasChildren</c>,
    /// and answers an empty pattern with the delimiter's line.</returns>
    public static IReadOnlyList<string> Lines(string command, IReadOnlyList<string> mailboxNames, string pattern)
    {
        var isList = command == "LIST";
        if (pattern.Length == 0)
        {
            return isList ? [@"* LIST (\Noselect) ""/"" """""] : [];
        }

        var mailboxes = mailboxNames.ToHashSet(StringComparer.Ordinal);
        var parents = isList ? mailboxNames.SelectMany(ParentLevels).Where(parent => !mailboxes.Contains(parent)) : [];
        return mailboxNames.Concat(parents)
            .Distinct(StringComparer.Ordinal)
            .Where(name => Matches(name, pattern))
            .OrderBy(name => name != "INBOX")
            .ThenBy(name => name, StringComparer.Ordinal)
            .Select(name => $"* {command} ({Attributes(name, mailboxes, mailboxNames)}) \"/\" {ImapMailboxName.ToWire(name)}")
            .ToList();
    }

    /// <summary>
    /// Whether <paramref name="name"/> matches <paramref name="pattern"/>: <c>*</c> matches any
    /// run of characters, <c>%</c> any run without <c>/</c>, and every other character itself -
    /// ordinally, except that <c>INBOX</c> matches without regard to case.
    /// </summary>
    /// <param name="name">A mailbox name.</param>
    /// <param name="pattern">The reference and the pattern joined.</param>
    /// <returns>Whether it matches.</returns>
    public static bool Matches(string name, string pattern)
    {
        var comparer = name == "INBOX" ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        // matched[i]: whether the pattern read so far matches the first i characters of the name.
        var matched = new bool[name.Length + 1];
        matched[0] = true;
        foreach (var token in pattern)
        {
            matched = token is '*' or '%' ? MatchWildcard(name, matched, token) : MatchCharacter(name, matched, token, comparer);
        }

        return matched[name.Length];
    }

    private static bool[] MatchWildcard(string name, bool[] matched, char wildcard)
    {
        var next = new bool[matched.Length];
        next[0] = matched[0];
        for (var index = 1; index < matched.Length; index++)
        {
            next[index] = matched[index] || (next[index - 1] && (wildcard == '*' || name[index - 1] != ImapMailboxName.Delimiter));
        }

        return next;
    }

    private static bool[] MatchCharacter(string name, bool[] matched, char character, StringComparer comparer)
    {
        var next = new bool[matched.Length];
        for (var index = 1; index < matched.Length; index++)
        {
            next[index] = matched[index - 1] && comparer.Equals(name[index - 1].ToString(), character.ToString());
        }

        return next;
    }

    private static IEnumerable<string> ParentLevels(string name)
    {
        for (var slash = name.IndexOf(ImapMailboxName.Delimiter); slash > 0; slash = name.IndexOf(ImapMailboxName.Delimiter, slash + 1))
        {
            yield return name[..slash];
        }
    }

    private static string Attributes(string name, HashSet<string> mailboxes, IReadOnlyList<string> mailboxNames)
    {
        var prefix = name + ImapMailboxName.Delimiter;
        var hasChildren = mailboxNames.Any(other => other.StartsWith(prefix, StringComparison.Ordinal));
        return !mailboxes.Contains(name) ? @"\Noselect \HasChildren"
            : hasChildren ? @"\HasChildren"
            : @"\HasNoChildren";
    }
}
