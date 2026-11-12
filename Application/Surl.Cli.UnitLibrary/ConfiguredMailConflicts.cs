namespace Surl.Cli;

/// <summary>
/// The checks ADR-0101 decision 5 makes on <c>--mail-alias</c>, <c>--mail-catch-all</c> and <c>--shared-folder</c>
/// once the whole command line, every config file included, is read. Each item comes with the option name it was
/// written with (<c>--mail-alias</c>, or <c>mail-alias</c> on a config-file line), the name a refusal starts with.
/// In order: an alias address, a catch-all domain and a shared folder name given twice (row 14); then an alias with
/// no <c>--mail-domain</c>, outside every one, or also a mailbox, and a catch-all domain with no <c>--mail-domain</c>
/// or not one of them (row 15). Addresses and domains compare as <see cref="MailDomainNames"/> normalises them;
/// an alias and a mailbox address without regard to case, as two mailboxes do; folder names ordinally. Each
/// refusal names the address, domain or folder as written.
/// </summary>
internal static class ConfiguredMailConflicts
{
    /// <summary>One refusal: the option name as written and the reason after <c>option &lt;name&gt;: </c>.</summary>
    /// <param name="OptionName">The option name the refused item was written with.</param>
    /// <param name="Reason">The refusal reason.</param>
    public sealed record Refusal(string OptionName, string Reason);

    /// <summary>An alias or catch-all given with no <c>--mail-domain</c> at all (row 15).</summary>
    /// <param name="noun">The noun and value, e.g. <c>alias ann@example.com</c>.</param>
    /// <returns><c>&lt;noun&gt; needs --mail-domain</c>.</returns>
    public static string NeedsMailDomain(string noun) => $"{noun} needs --mail-domain";

    /// <summary>An alias address or catch-all domain no <c>--mail-domain</c> names (row 15).</summary>
    /// <param name="noun">The noun and value, e.g. <c>alias ann@example.org</c>.</param>
    /// <returns><c>&lt;noun&gt; is not in any --mail-domain</c>.</returns>
    public static string NotInAnyMailDomain(string noun) => $"{noun} is not in any --mail-domain";

    /// <summary>An alias address that is also a mailbox's (row 15).</summary>
    /// <param name="address">The alias address as written.</param>
    /// <returns><c>alias &lt;address&gt; is also a mailbox</c>.</returns>
    public static string AliasIsAlsoAMailbox(string address) => $"alias {address} is also a mailbox";

    /// <summary>Finds the first refusal, or none.</summary>
    /// <param name="mailDomains">The <c>--mail-domain</c>s as written, each one <see cref="MailDomainNames.ReadDomain"/> accepts.</param>
    /// <param name="mailboxes">The mailboxes, their addresses normalised.</param>
    /// <param name="aliases">Each <c>--mail-alias</c> with its option name, in configuration order.</param>
    /// <param name="catchAlls">Each <c>--mail-catch-all</c> with its option name, in configuration order.</param>
    /// <param name="sharedFolders">Each <c>--shared-folder</c> with its option name, in configuration order.</param>
    /// <returns>The first refusal, or <see langword="null"/>.</returns>
    public static Refusal? Find(
        IReadOnlyList<string> mailDomains,
        IReadOnlyList<ConfiguredMailbox> mailboxes,
        IReadOnlyList<(string OptionName, ConfiguredMailAlias Alias)> aliases,
        IReadOnlyList<(string OptionName, ConfiguredMailCatchAll CatchAll)> catchAlls,
        IReadOnlyList<(string OptionName, ConfiguredSharedFolder Folder)> sharedFolders)
    {
        ArgumentNullException.ThrowIfNull(mailDomains);
        ArgumentNullException.ThrowIfNull(mailboxes);
        ArgumentNullException.ThrowIfNull(aliases);
        ArgumentNullException.ThrowIfNull(catchAlls);
        ArgumentNullException.ThrowIfNull(sharedFolders);
        var domains = mailDomains.Select(NormalisedDomain).ToHashSet(StringComparer.Ordinal);
        var mailboxAddresses = mailboxes.Select(mailbox => mailbox.Address).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return FindRepeat(aliases.Select(alias => (alias.OptionName, alias.Alias.Address, NormalisedAddress(alias.Alias.Address))), ConfiguredMailAliasReader.AliasGivenTwice)
            ?? FindRepeat(catchAlls.Select(catchAll => (catchAll.OptionName, catchAll.CatchAll.Domain, NormalisedDomain(catchAll.CatchAll.Domain))), ConfiguredMailCatchAllReader.CatchAllDomainGivenTwice)
            ?? FindRepeat(sharedFolders.Select(folder => (folder.OptionName, folder.Folder.Name, folder.Folder.Name)), ConfiguredSharedFolderReader.SharedFolderGivenTwice)
            ?? FindFirst(aliases, alias => FindAliasConflict(alias.Item.Address, domains, mailboxAddresses))
            ?? FindFirst(catchAlls, catchAll => FindDomainConflict("catch-all domain " + catchAll.Item.Domain, NormalisedDomain(catchAll.Item.Domain), domains));
    }

    private static string? FindAliasConflict(string written, HashSet<string> domains, HashSet<string> mailboxAddresses)
    {
        var address = NormalisedAddress(written);
        return FindDomainConflict("alias " + written, address[(address.LastIndexOf('@') + 1)..], domains)
            ?? (mailboxAddresses.Contains(address) ? AliasIsAlsoAMailbox(written) : null);
    }

    private static string? FindDomainConflict(string noun, string domain, HashSet<string> domains)
    {
        if (domains.Count == 0)
        {
            return NeedsMailDomain(noun);
        }

        return domains.Contains(domain) ? null : NotInAnyMailDomain(noun);
    }

    private static Refusal? FindFirst<T>(IEnumerable<(string OptionName, T Item)> items, Func<(string OptionName, T Item), string?> findConflict)
    {
        foreach (var item in items)
        {
            if (findConflict(item) is { } reason)
            {
                return new(item.OptionName, reason);
            }
        }

        return null;
    }

    private static Refusal? FindRepeat(IEnumerable<(string OptionName, string Written, string Compared)> named, Func<string, string> givenTwice)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (optionName, written, compared) in named)
        {
            if (!seen.Add(compared))
            {
                return new(optionName, givenTwice(written));
            }
        }

        return null;
    }

    private static string NormalisedDomain(string written)
    {
        MailDomainNames.ReadDomain(written, out var domain);
        return domain;
    }

    private static string NormalisedAddress(string written)
    {
        MailDomainNames.ReadAddress(written, out var address);
        return address;
    }
}
