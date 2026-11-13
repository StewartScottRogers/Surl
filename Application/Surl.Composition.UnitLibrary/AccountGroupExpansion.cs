using System.Collections.Frozen;
using Surl.Cli;
using Surl.Output;

namespace Surl.Composition;

/// <summary>
/// Every <c>--group</c> expanded to the account names it holds (ADR-0102 decision 4.1): composed at
/// start, after <c>--user-file</c> is read and before any listener binds, so no store, server or
/// contract ever sees a group. Groups nest; an account reached twice counts once. The later
/// member-list options (<c>--shared-folder</c>, <c>--share-access</c>, <c>--topic-access</c>) turn
/// their members into account names with <see cref="Expand"/>.
/// </summary>
public sealed class AccountGroupExpansion
{
    /// <summary>The reserved member meaning every caller, and the group name no <c>--group</c> may take (ADR-0102 decision 4.2).</summary>
    public const string Everyone = "everyone";

    private const string GroupOption = "group";

    private readonly FrozenSet<string> accountNames;
    private readonly IReadOnlyList<KeyValuePair<string, FrozenSet<string>>> groupsInOrder;
    private readonly FrozenDictionary<string, FrozenSet<string>> accountsByGroup;

    // Account names that are not configured accounts but were taken as directory accounts (ADR-0149 decision 8), in first-seen order.
    private readonly List<string> directoryNames = [];
    private readonly bool takesUnknownNamesAsDirectoryAccounts;

    private AccountGroupExpansion(
        FrozenSet<string> accountNames,
        IReadOnlyList<KeyValuePair<string, FrozenSet<string>>> groupsInOrder,
        bool takesUnknownNamesAsDirectoryAccounts)
    {
        this.takesUnknownNamesAsDirectoryAccounts = takesUnknownNamesAsDirectoryAccounts;
        this.accountNames = accountNames;
        this.groupsInOrder = groupsInOrder;
        accountsByGroup = groupsInOrder.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>Each group's name and the account names it holds, in command-line order.</summary>
    public IReadOnlyList<KeyValuePair<string, FrozenSet<string>>> Groups => groupsInOrder;

    /// <summary>
    /// Expands <paramref name="groups"/> over <paramref name="accountNames"/>, refusing, in this
    /// order and each at its first instance in command-line order: a group named <c>everyone</c> or
    /// a member <c>@everyone</c> (ADR-0102 decision 4.2), a member naming no account or group, and a
    /// group that includes itself directly or through other groups (ADR-0101 decision 5 row 18).
    /// </summary>
    /// <param name="groups">Every <c>--group</c>, in command-line order, no name twice.</param>
    /// <param name="accountNames">Every account's name: <c>--user</c>, then <c>--user-file</c>.</param>
    /// <param name="takesUnknownNamesAsDirectoryAccounts">
    /// Whether <c>--user-ldap</c> is given: a member naming no configured account is then accepted as a
    /// directory account, since the directory is not read at start (ADR-0149 decision 8, amending ADR-0102 decision 4.1).
    /// </param>
    /// <returns>The expansion; or null and the refusal after <c>surl: </c>, which exits <c>FailedInit</c> (2).</returns>
    public static (AccountGroupExpansion? Expansion, string? FailureMessage) Compose(
        IReadOnlyList<ConfiguredGroup> groups, IEnumerable<string> accountNames, bool takesUnknownNamesAsDirectoryAccounts = false)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(accountNames);
        var accounts = accountNames.ToFrozenSet(StringComparer.Ordinal);
        var membersByGroup = groups.ToDictionary(group => group.Name, group => group.Members, StringComparer.Ordinal);
        var expanded = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal);
        var failure = RefuseEveryone(groups)
            ?? RefuseUnknownMember(groups, accounts, membersByGroup, takesUnknownNamesAsDirectoryAccounts)
            ?? ExpandEveryGroup(groups, membersByGroup, expanded);
        if (failure is not null)
        {
            return (null, failure);
        }

        var expansion = new AccountGroupExpansion(
            accounts, [.. groups.Select(group => KeyValuePair.Create(group.Name, expanded[group.Name]))], takesUnknownNamesAsDirectoryAccounts);
        expansion.directoryNames.AddRange(groups.SelectMany(group => group.Members).Where(member => !member.IsGroup && !accounts.Contains(member.Name)).Select(member => member.Name).Distinct(StringComparer.Ordinal));

        return (expansion, null);
    }

    /// <summary>A member naming no account or group (ADR-0101 decision 5 row 18).</summary>
    /// <param name="optionName">The option's long name, without <c>--</c>.</param>
    /// <param name="member">The member as written, <c>@</c> included for a group.</param>
    /// <returns><c>(2) option --&lt;option&gt;: member &lt;member&gt; names no account or group</c>.</returns>
    public static string MemberNamesNothing(string optionName, string member) =>
        $"(2) option --{optionName}: member {member} names no account or group";

    /// <summary>A group that includes itself, directly or through other groups (ADR-0101 decision 5 row 18).</summary>
    /// <param name="name">The group's name.</param>
    /// <returns><c>(2) option --group: group &lt;name&gt; includes itself</c>.</returns>
    public static string GroupIncludesItself(string name) => $"(2) option --{GroupOption}: group {name} includes itself";

    /// <summary>A group named <c>everyone</c> (ADR-0102 decision 4.2).</summary>
    public static string EveryoneIsReserved => $"(2) option --{GroupOption}: group {Everyone} is reserved";

    /// <summary><c>@everyone</c> in a member list that does not take it (ADR-0102 decision 4.2).</summary>
    /// <param name="optionName">The option's long name, without <c>--</c>.</param>
    /// <returns><c>(2) option --&lt;option&gt;: member @everyone is not allowed here</c>.</returns>
    public static string EveryoneIsNotAllowed(string optionName) => $"(2) option --{optionName}: member @{Everyone} is not allowed here";

    /// <summary>
    /// Turns a member list into the account names it reaches: an account member as itself, a group
    /// member as the accounts its group holds, each account once. <c>@everyone</c> is the caller's to
    /// handle before this, since only some options take it.
    /// </summary>
    /// <param name="optionName">The option the list belongs to, its long name without <c>--</c>, for the refusal.</param>
    /// <param name="members">The members as written.</param>
    /// <param name="accounts">The account names reached; empty when refused.</param>
    /// <returns>Null, or the refusal after <c>surl: </c> for the first member naming no account or group.</returns>
    public string? Expand(string optionName, IReadOnlyList<ConfiguredMember> members, out IReadOnlySet<string> accounts)
    {
        ArgumentNullException.ThrowIfNull(optionName);
        ArgumentNullException.ThrowIfNull(members);
        var reached = new HashSet<string>(StringComparer.Ordinal);
        accounts = reached;
        foreach (var member in members)
        {
            if (!TryReach(member, reached))
            {
                reached.Clear();
                return MemberNamesNothing(optionName, member.AsWritten);
            }
        }

        return null;
    }

    /// <summary>
    /// Writes ADR-0102 decision 5's note for each group, <c>* Group &lt;name&gt;: &lt;n&gt; accounts</c>,
    /// in command-line order, from the verbose level up.
    /// </summary>
    /// <param name="logLevel">The log level.</param>
    /// <param name="log">The log stream.</param>
    public void WriteStartLines(LogLevel logLevel, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (logLevel < LogLevel.Verbose)
        {
            return;
        }

        foreach (var (name, accounts) in groupsInOrder)
        {
            log.WriteLine($"* Group {name}: {accounts.Count} accounts");
        }

        foreach (var name in directoryNames)
        {
            log.WriteLine($"Member {name} is not a configured account; taken as a directory account");
        }
    }

    private bool TryReach(ConfiguredMember member, HashSet<string> reached)
    {
        IReadOnlySet<string>? memberAccounts = member.IsGroup
            ? accountsByGroup.GetValueOrDefault(member.Name)
            : ReachAccount(member.Name);
        if (memberAccounts is null)
        {
            return false;
        }

        reached.UnionWith(memberAccounts);
        return true;
    }

    // An account, or, when a directory is the account source, any name: the directory is not read at start (ADR-0149 decision 8).
    private HashSet<string>? ReachAccount(string name)
    {
        if (!accountNames.Contains(name))
        {
            if (!takesUnknownNamesAsDirectoryAccounts)
            {
                return null;
            }

            if (!directoryNames.Contains(name))
            {
                directoryNames.Add(name);
            }
        }

        return new HashSet<string>(StringComparer.Ordinal) { name };
    }

    private static string? RefuseEveryone(IReadOnlyList<ConfiguredGroup> groups)
    {
        foreach (var group in groups)
        {
            if (group.Name == Everyone)
            {
                return EveryoneIsReserved;
            }

            if (group.Members.Any(member => member.IsGroup && member.Name == Everyone))
            {
                return EveryoneIsNotAllowed(GroupOption);
            }
        }

        return null;
    }

    private static string? RefuseUnknownMember(
        IReadOnlyList<ConfiguredGroup> groups,
        FrozenSet<string> accounts,
        Dictionary<string, IReadOnlyList<ConfiguredMember>> membersByGroup,
        bool takesUnknownNamesAsDirectoryAccounts)
    {
        var unknown = groups
            .SelectMany(group => group.Members)
            .FirstOrDefault(member => !(member.IsGroup ? membersByGroup.ContainsKey(member.Name) : takesUnknownNamesAsDirectoryAccounts || accounts.Contains(member.Name)));
        return unknown is null ? null : MemberNamesNothing(GroupOption, unknown.AsWritten);
    }

    /// <summary>Expands each group in command-line order into <paramref name="expanded"/>; the first cycle's refusal, or null.</summary>
    private static string? ExpandEveryGroup(
        IReadOnlyList<ConfiguredGroup> groups,
        Dictionary<string, IReadOnlyList<ConfiguredMember>> membersByGroup,
        Dictionary<string, FrozenSet<string>> expanded) =>
        groups
            .Select(group => ExpandGroup(group.Name, membersByGroup, expanded, new HashSet<string>(StringComparer.Ordinal)))
            .FirstOrDefault(failure => failure is not null);

    /// <summary>
    /// Expands one group depth first into <paramref name="expanded"/>, its nested groups first;
    /// <paramref name="including"/> holds the groups being expanded on the way down, so meeting one
    /// again is a cycle, refused naming the group met again.
    /// </summary>
    private static string? ExpandGroup(
        string name,
        Dictionary<string, IReadOnlyList<ConfiguredMember>> membersByGroup,
        Dictionary<string, FrozenSet<string>> expanded,
        HashSet<string> including)
    {
        if (expanded.ContainsKey(name))
        {
            return null;
        }

        if (!including.Add(name))
        {
            return GroupIncludesItself(name);
        }

        var accounts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in membersByGroup[name])
        {
            if (!member.IsGroup)
            {
                accounts.Add(member.Name);
                continue;
            }

            var failure = ExpandGroup(member.Name, membersByGroup, expanded, including);
            if (failure is not null)
            {
                return failure;
            }

            accounts.UnionWith(expanded[member.Name]);
        }

        including.Remove(name);
        expanded[name] = accounts.ToFrozenSet(StringComparer.Ordinal);
        return null;
    }
}
