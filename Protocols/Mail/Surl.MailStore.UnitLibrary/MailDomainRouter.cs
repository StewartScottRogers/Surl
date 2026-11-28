namespace Surl.MailStore;

/// <summary>
/// Resolves an address in a configured mail domain to its owners in ADR-0102 decision 1.3's
/// order, the first step that matches deciding: an alias, each of its targets resolved again;
/// a mailbox; an account named by the local part; the domain's catch-all, its target resolved
/// again; or none. Every owner is found once, however many routes reach it.
/// </summary>
internal sealed class MailDomainRouter
{
    private readonly IReadOnlySet<string> domains;
    private readonly IReadOnlyList<MailAlias> configuredAliases;
    private readonly LocalPartLookup<MailAlias> aliases = new();
    private readonly LocalPartLookup<OwnerMailboxes> mailboxes;
    private readonly IReadOnlyDictionary<string, string> catchAlls;
    private readonly Func<string, OwnerMailboxes?> findAccount;

    /// <param name="configuration">The configuration, with at least one domain.</param>
    /// <param name="mailboxes">Each mailbox's owner, by its normalised address.</param>
    /// <param name="findAccount">Finds the account a local part names, as ADR-0050 decision 5 matches it.</param>
    public MailDomainRouter(
        MailDomainConfiguration configuration,
        LocalPartLookup<OwnerMailboxes> mailboxes,
        Func<string, OwnerMailboxes?> findAccount)
    {
        domains = configuration.Domains;
        configuredAliases = configuration.Aliases;
        catchAlls = configuration.CatchAlls;
        this.mailboxes = mailboxes;
        this.findAccount = findAccount;
        foreach (var alias in configuredAliases)
        {
            aliases.Add(alias.Address, alias);
        }
    }

    /// <summary>
    /// Whether <paramref name="domain"/>, normalised, is a configured domain.
    /// </summary>
    public bool Serves(string domain) => domains.Contains(domain);

    /// <summary>
    /// Checks that every alias and catch-all resolves to owners.
    /// </summary>
    /// <exception cref="MailRouteException">One does not.</exception>
    public void ThrowIfRouteUnresolvable()
    {
        foreach (var route in configuredAliases.Select(AliasRoute).Concat(catchAlls.Select(CatchAllRoute)))
        {
            var problem = Expand(route, new Expansion());
            if (problem is not null)
            {
                throw problem;
            }
        }
    }

    /// <summary>
    /// The owners <paramref name="address"/> resolves to, each once; none when it resolves to
    /// none (step 5). A target of an alias or catch-all that no longer names an owner, as one
    /// can after an owner is added or retired (ADR-0185), adds none, and the other targets are
    /// still resolved.
    /// </summary>
    public IReadOnlyList<OwnerMailboxes> Resolve(MailDomainAddress address)
    {
        var expansion = new Expansion();
        _ = Resolve(address, expansion, out _);
        return expansion.Owners;
    }

    private static Route AliasRoute(MailAlias alias) => new(MailRouteKind.Alias, "alias " + alias.Address, alias.Address, alias.Targets);

    private static Route CatchAllRoute(KeyValuePair<string, string> catchAll) =>
        new(MailRouteKind.CatchAll, "catch-all " + catchAll.Key, catchAll.Value, [catchAll.Value]);

    /// <param name="address">The address to resolve, in a configured domain.</param>
    /// <param name="expansion">What has been expanded and found on the way here.</param>
    /// <param name="namesNoOwner">Set when the address resolved to nothing at step 5.</param>
    /// <returns>The first problem a route on the way has, or <see langword="null"/>.</returns>
    private MailRouteException? Resolve(MailDomainAddress address, Expansion expansion, out bool namesNoOwner)
    {
        namesNoOwner = false;
        var name = address.ToString();
        var alias = aliases.Find(name);
        if (alias is not null)
        {
            return Expand(AliasRoute(alias), expansion);
        }

        var owner = mailboxes.Find(name) ?? findAccount(address.LocalPart);
        if (owner is not null)
        {
            if (!expansion.Owners.Contains(owner))
            {
                expansion.Owners.Add(owner);
            }

            return null;
        }

        if (catchAlls.TryGetValue(address.Domain, out var target))
        {
            return Expand(CatchAllRoute(new(address.Domain, target)), expansion);
        }

        namesNoOwner = true;
        return null;
    }

    /// <summary>
    /// Resolves every target of <paramref name="route"/>, whatever problems the earlier ones
    /// have, and returns the first problem in target order. A route already expanded is not
    /// expanded again: its owners are found, and following every path to it could take
    /// factorially long.
    /// </summary>
    private MailRouteException? Expand(Route route, Expansion expansion)
    {
        if (expansion.RoutesExpanded.Contains(route.Key))
        {
            return null;
        }

        if (!expansion.RoutesOnPath.Add(route.Key))
        {
            return new MailRouteException(route.Kind, MailRouteProblem.IncludesItself, route.Address);
        }

        var problems = route.Targets.Select(target => ResolveTarget(route, target, expansion)).ToList();
        expansion.RoutesOnPath.Remove(route.Key);
        expansion.RoutesExpanded.Add(route.Key);
        return problems.Find(problem => problem is not null);
    }

    private MailRouteException? ResolveTarget(Route route, string target, Expansion expansion)
    {
        if (!MailDomainAddress.TryParse(target, out var address) || !Serves(address.Domain))
        {
            return new MailRouteException(route.Kind, MailRouteProblem.TargetOutsideMailDomains, target);
        }

        var problem = Resolve(address, expansion, out var namesNoOwner);
        return namesNoOwner ? new MailRouteException(route.Kind, MailRouteProblem.TargetNamesNoMailbox, target) : problem;
    }

    /// <param name="Kind">The kind of route.</param>
    /// <param name="Key">The route's identity on a path, to find a loop.</param>
    /// <param name="Address">The address a loop's refusal names: an alias's own, a catch-all's target.</param>
    /// <param name="Targets">The addresses it delivers to.</param>
    private sealed record Route(MailRouteKind Kind, string Key, string Address, IReadOnlyList<string> Targets);

    /// <summary>The state of resolving one address or checking one route.</summary>
    private sealed class Expansion
    {
        /// <summary>Each owner found, once, in the order first found.</summary>
        public List<OwnerMailboxes> Owners { get; } = [];

        /// <summary>The keys of the routes being expanded on the way here, to find a loop.</summary>
        public HashSet<string> RoutesOnPath { get; } = [];

        /// <summary>The keys of the routes whose every target has been resolved.</summary>
        public HashSet<string> RoutesExpanded { get; } = [];
    }
}
