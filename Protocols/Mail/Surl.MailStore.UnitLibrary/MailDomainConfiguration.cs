namespace Surl.MailStore;

/// <summary>
/// The mail domains, mailboxes, aliases and catch-alls a <see cref="MailboxStore"/> resolves
/// recipients against (ADR-0102, decisions 1.1 to 1.3). Every domain and address is normalised
/// by <see cref="MailDomainName.TryNormalise"/> when it is made. With no domain, the store
/// resolves recipients as ADR-0050 decision 5 says.
/// </summary>
public sealed class MailDomainConfiguration
{
    /// <summary>
    /// Creates the configuration.
    /// </summary>
    /// <param name="domains">The <c>--mail-domain</c> names; a repeated one is one domain.</param>
    /// <param name="mailboxAddresses">The <c>--mailbox</c> addresses, each in a configured
    /// domain; each names one owner and one account, by its normalised address.</param>
    /// <param name="aliases">The <c>--mail-alias</c> routes, each in a configured domain.
    /// Whether their targets resolve is checked by the store, which knows the accounts.</param>
    /// <param name="catchAlls">The <c>--mail-catch-all</c> routes, at most one per configured
    /// domain.</param>
    /// <param name="mailboxSettings">The <c>quota=</c> and <c>folders=</c> of mailboxes
    /// (ADR-0102, decision 1.4), at most one per mailbox address; <see langword="null"/> for
    /// none.</param>
    /// <param name="sharedFolders">The <c>--shared-folder</c>s (ADR-0102, decision 1.6), each
    /// name once; <see langword="null"/> for none. They need no domain.</param>
    /// <exception cref="ArgumentNullException">An argument, or an element of one, is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A domain is no domain name; an address is not
    /// <c>local@domain</c> or is outside every configured domain; an address is both a mailbox
    /// and an alias, or named twice as either; a domain has two catch-alls or none is
    /// configured for it; a mailbox's settings name no configured mailbox, are given twice,
    /// hold a quota below 1 or a folder name ADR-0050 decision 3 refuses; or a shared folder's
    /// name is refused, is <c>INBOX</c> in any case, or is given twice.</exception>
    public MailDomainConfiguration(
        IEnumerable<string> domains,
        IEnumerable<string> mailboxAddresses,
        IEnumerable<MailAlias> aliases,
        IEnumerable<MailCatchAll> catchAlls,
        IEnumerable<MailboxSettings>? mailboxSettings = null,
        IEnumerable<MailSharedFolder>? sharedFolders = null)
    {
        ArgumentNullException.ThrowIfNull(domains);
        ArgumentNullException.ThrowIfNull(mailboxAddresses);
        ArgumentNullException.ThrowIfNull(aliases);
        ArgumentNullException.ThrowIfNull(catchAlls);

        Domains = new HashSet<string>(domains.Select(NormaliseDomain), StringComparer.Ordinal);
        MailboxAddresses = [.. mailboxAddresses.Select(address => ConfiguredAddress(address).ToString())];
        Aliases = [.. aliases.Select(NormaliseAlias)];
        CatchAlls = catchAlls.Select(NormaliseCatchAll).ToDictionary(catchAll => catchAll.Domain, catchAll => catchAll.Target, StringComparer.Ordinal);
        ThrowIfAddressRepeated();
        MailboxSettings = (mailboxSettings ?? []).Select(NormaliseSettings).ToDictionary(settings => settings.Address, StringComparer.Ordinal);
        SharedFolders = [.. (sharedFolders ?? []).Select(NormaliseSharedFolder)];
        ThrowIfSharedFolderRepeated();
    }

    /// <summary>
    /// The configured domains, normalised.
    /// </summary>
    public IReadOnlySet<string> Domains { get; }

    /// <summary>
    /// The mailbox addresses, normalised: each is an owner's and an account's name.
    /// </summary>
    public IReadOnlyList<string> MailboxAddresses { get; }

    /// <summary>
    /// The aliases, their address and targets normalised.
    /// </summary>
    public IReadOnlyList<MailAlias> Aliases { get; }

    /// <summary>
    /// Each catch-all's normalised target, by its normalised domain.
    /// </summary>
    public IReadOnlyDictionary<string, string> CatchAlls { get; }

    /// <summary>
    /// Each mailbox's <c>quota=</c> and <c>folders=</c>, by its normalised address; a mailbox
    /// without settings is not here.
    /// </summary>
    public IReadOnlyDictionary<string, MailboxSettings> MailboxSettings { get; }

    /// <summary>
    /// The shared folders, each member that is a mailbox's address normalised.
    /// </summary>
    public IReadOnlyList<MailSharedFolder> SharedFolders { get; }

    private MailboxSettings NormaliseSettings(MailboxSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settings.Folders);
        var address = ConfiguredAddress(settings.Address).ToString();
        if (!MailboxAddresses.Contains(address, StringComparer.Ordinal))
        {
            throw new ArgumentException($"The mail address {settings.Address} is no mailbox.", nameof(settings));
        }

        if (settings.QuotaBytes < 1)
        {
            throw new ArgumentException($"The quota of {address} is not above 0.", nameof(settings));
        }

        return new MailboxSettings(address, settings.QuotaBytes, [.. settings.Folders.Select(ValidFolderName)]);
    }

    private static string ValidFolderName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return MailboxName.IsValid(name) ? name : throw new ArgumentException($"The folder name {name} is not a mailbox name.", nameof(name));
    }

    private MailSharedFolder NormaliseSharedFolder(MailSharedFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(folder.Members);
        var name = ValidFolderName(folder.Name);
        return MailboxName.Canonical(name) == MailboxName.Inbox
            ? throw new ArgumentException("INBOX cannot be a shared folder.", nameof(folder))
            : new MailSharedFolder(name, [.. folder.Members.Select(NormaliseMember)]);
    }

    /// <summary>
    /// A member that is an address in a configured domain is a mailbox's account, named by its
    /// normalised address; any other member is an account's name as given.
    /// </summary>
    private string NormaliseMember(string member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return MailDomainAddress.TryParse(member, out var parsed) && Domains.Contains(parsed.Domain) ? parsed.ToString() : member;
    }

    private void ThrowIfSharedFolderRepeated()
    {
        var repeated = SharedFolders.GroupBy(folder => folder.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (repeated is not null)
        {
            throw new ArgumentException($"The shared folder {repeated.Key} is configured twice.", nameof(SharedFolders));
        }
    }

    private static string NormaliseDomain(string domain) =>
        MailDomainName.TryNormalise(domain, out var asciiDomain)
            ? asciiDomain
            : throw new ArgumentException($"The mail domain {domain} is not a domain name.", nameof(domain));

    private MailDomainAddress ConfiguredAddress(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!MailDomainAddress.TryParse(address, out var parsed))
        {
            throw new ArgumentException($"The mail address {address} is not local@domain.", nameof(address));
        }

        return Domains.Contains(parsed.Domain)
            ? parsed
            : throw new ArgumentException($"The mail address {address} is not in any mail domain.", nameof(address));
    }

    private MailAlias NormaliseAlias(MailAlias alias)
    {
        ArgumentNullException.ThrowIfNull(alias);
        ArgumentNullException.ThrowIfNull(alias.Targets);
        return new MailAlias(ConfiguredAddress(alias.Address).ToString(), [.. alias.Targets.Select(NormaliseTarget)]);
    }

    private MailCatchAll NormaliseCatchAll(MailCatchAll catchAll)
    {
        ArgumentNullException.ThrowIfNull(catchAll);
        var domain = NormaliseDomain(catchAll.Domain);
        return Domains.Contains(domain)
            ? new MailCatchAll(domain, NormaliseTarget(catchAll.Target))
            : throw new ArgumentException($"The catch-all's domain {catchAll.Domain} is not a mail domain.", nameof(catchAll));
    }

    /// <summary>
    /// A target is normalised but not checked: one that is not <c>local@domain</c>, or is outside
    /// every configured domain, is kept and reported by the store as
    /// <see cref="MailRouteProblem.TargetOutsideMailDomains"/>.
    /// </summary>
    private static string NormaliseTarget(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return MailDomainAddress.TryParse(target, out var parsed) ? parsed.ToString() : target;
    }

    private void ThrowIfAddressRepeated()
    {
        var repeated = MailboxAddresses.Concat(Aliases.Select(alias => alias.Address))
            .GroupBy(address => address, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (repeated is not null)
        {
            throw new ArgumentException($"The mail address {repeated.Key} is configured twice.", nameof(MailboxAddresses));
        }
    }
}
