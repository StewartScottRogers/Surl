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
            var problem = Expand(route, [], []);
            if (problem is not null)
            {
                throw problem;
            }
        }
    }

    /// <summary>
    /// The owners <paramref name="address"/> resolves to, each once; none when it resolves to
    /// none (step 5).
    /// </summary>
    public IReadOnlyList<OwnerMailboxes> Resolve(MailDomainAddress address)
    {
        var owners = new List<OwnerMailboxes>();
        _ = Resolve(address, [], owners, out _);
        return owners;
    }

    private static Route AliasRoute(MailAlias alias) => new(MailRouteKind.Alias, "alias " + alias.Address, alias.Address, alias.Targets);

    private static Route CatchAllRoute(KeyValuePair<string, string> catchAll) =>
        new(MailRouteKind.CatchAll, "catch-all " + catchAll.Key, catchAll.Value, [catchAll.Value]);

    /// <param name="address">The address to resolve, in a configured domain.</param>
    /// <param name="routesOnPath">The routes being expanded on the way here, to find a loop.</param>
    /// <param name="owners">Where each owner found is added once.</param>
    /// <param name="namesNoOwner">Set when the address resolved to nothing at step 5.</param>
    /// <returns>The problem a route on the way has, or <see langword="null"/>.</returns>
    private MailRouteException? Resolve(MailDomainAddress address, HashSet<string> routesOnPath, List<OwnerMailboxes> owners, out bool namesNoOwner)
    {
        namesNoOwner = false;
        var name = address.ToString();
        var alias = aliases.Find(name);
        if (alias is not null)
        {
            return Expand(AliasRoute(alias), routesOnPath, owners);
        }

        var owner = mailboxes.Find(name) ?? findAccount(address.LocalPart);
        if (owner is not null)
        {
            if (!owners.Contains(owner))
            {
                owners.Add(owner);
            }

            return null;
        }

        if (catchAlls.TryGetValue(address.Domain, out var target))
        {
            return Expand(CatchAllRoute(new(address.Domain, target)), routesOnPath, owners);
        }

        namesNoOwner = true;
        return null;
    }

    private MailRouteException? Expand(Route route, HashSet<string> routesOnPath, List<OwnerMailboxes> owners)
    {
        if (!routesOnPath.Add(route.Key))
        {
            return new MailRouteException(route.Kind, MailRouteProblem.IncludesItself, route.Address);
        }

        var problem = route.Targets.Select(target => ResolveTarget(route, target, routesOnPath, owners)).FirstOrDefault(problem => problem is not null);
        routesOnPath.Remove(route.Key);
        return problem;
    }

    private MailRouteException? ResolveTarget(Route route, string target, HashSet<string> routesOnPath, List<OwnerMailboxes> owners)
    {
        if (!MailDomainAddress.TryParse(target, out var address) || !Serves(address.Domain))
        {
            return new MailRouteException(route.Kind, MailRouteProblem.TargetOutsideMailDomains, target);
        }

        var problem = Resolve(address, routesOnPath, owners, out var namesNoOwner);
        return namesNoOwner ? new MailRouteException(route.Kind, MailRouteProblem.TargetNamesNoMailbox, target) : problem;
    }

    /// <param name="Kind">The kind of route.</param>
    /// <param name="Key">The route's identity on a path, to find a loop.</param>
    /// <param name="Address">The address a loop's refusal names: an alias's own, a catch-all's target.</param>
    /// <param name="Targets">The addresses it delivers to.</param>
    private sealed record Route(MailRouteKind Kind, string Key, string Address, IReadOnlyList<string> Targets);
}
