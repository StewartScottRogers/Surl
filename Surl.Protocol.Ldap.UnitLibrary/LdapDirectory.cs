using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// The directory the LDAP server serves (ADR-0072 decision 1): entries in their source's order,
/// read-only once built and so safe for concurrent searches, bounded, and searched with base,
/// one-level and subtree scopes. A search of the empty DN answers the root DSE.
/// </summary>
internal sealed class LdapDirectory
{
    /// <summary>The most entries a directory holds by default.</summary>
    public const int DefaultMaxEntries = 100000;

    /// <summary>The most bytes of DNs, descriptions and values a directory holds by default (256 MiB).</summary>
    public const long DefaultMaxTotalBytes = 268435456;

    /// <summary>The most bytes of one value; a value past <c>--max-message</c> could never be sent.</summary>
    public const int MaxValueBytes = 1048576;

    /// <summary>The most entries one search returns by default.</summary>
    public const int DefaultMaxSearchEntries = 10000;

    private const string RootDseExtensionStartTls = "1.3.6.1.4.1.1466.20037";

    private static readonly IReadOnlyList<LdapAttribute> RootDseUserAttributes = [Attribute("objectClass", "top")];

    private readonly Dictionary<string, LdapEntry> entriesByNormalForm = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private readonly int maxSearchEntries;

    /// <summary>
    /// Initializes a new instance of the <see cref="LdapDirectory"/> class.
    /// </summary>
    /// <param name="entries">The entries, in their source's order.</param>
    /// <param name="timeProvider">The clock a search's <c>timeLimit</c> is measured by.</param>
    /// <param name="maxEntries">The most entries the directory holds.</param>
    /// <param name="maxTotalBytes">The most bytes of DNs, descriptions and values the directory holds.</param>
    /// <param name="maxSearchEntries">The most entries one search returns.</param>
    /// <exception cref="LdapDirectoryException">An entry cannot be in a directory.</exception>
    public LdapDirectory(
        IReadOnlyList<LdapEntry> entries,
        TimeProvider timeProvider,
        int maxEntries = DefaultMaxEntries,
        long maxTotalBytes = DefaultMaxTotalBytes,
        int maxSearchEntries = DefaultMaxSearchEntries)
    {
        this.timeProvider = timeProvider;
        this.maxSearchEntries = maxSearchEntries;
        Entries = entries;
        long totalBytes = 0;
        for (var index = 0; index < entries.Count; index++)
        {
            totalBytes += SizeOf(entries[index]);
            var fault = FaultOf(entries[index], index < maxEntries && totalBytes <= maxTotalBytes);
            if (fault is not null)
            {
                throw new LdapDirectoryException(fault.Value, index);
            }

            entriesByNormalForm.Add(entries[index].Dn.NormalForm, entries[index]);
        }

        ThrowOnAMissingParent();
        NamingContexts = entries.Where(entry => FindNearestSuperior(entry.Dn) is null)
            .Select(entry => entry.Dn.Text)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Gets the entries, in their source's order.</summary>
    public IReadOnlyList<LdapEntry> Entries { get; }

    /// <summary>Gets the DNs, as written and in ordinal order ignoring case, of the entries with no superior in the directory.</summary>
    public IReadOnlyList<string> NamingContexts { get; }

    /// <summary>
    /// Finds the entry a DN names, in normal form.
    /// </summary>
    /// <param name="dn">The DN.</param>
    /// <returns>The entry; <see langword="null"/> when the directory has none.</returns>
    public LdapEntry? FindEntry(LdapDistinguishedName dn) => entriesByNormalForm.GetValueOrDefault(dn.NormalForm);

    /// <summary>
    /// Answers a search: <c>invalidDNSyntax</c> for a base that is not a DN, the root DSE for the
    /// empty base, <c>noSuchObject</c> with the nearest existing superior as matched DN for a base
    /// the directory lacks, and otherwise the matching entries up to the size limit, in directory
    /// order, then <c>success</c>, <c>sizeLimitExceeded</c> or <c>timeLimitExceeded</c>.
    /// </summary>
    /// <param name="request">The search.</param>
    /// <param name="rootDseFacts">What the root DSE says on this connection.</param>
    /// <returns>The entries and the result.</returns>
    public LdapSearchOutcome Search(LdapSearchRequest request, LdapRootDseFacts rootDseFacts)
    {
        if (!LdapDistinguishedName.TryParse(request.BaseObject, out var baseDn))
        {
            return new LdapSearchOutcome([], new LdapResult(LdapResultCode.InvalidDnSyntax, string.Empty, "the base is not an RFC 4514 DN"));
        }

        if (baseDn.IsRoot)
        {
            return SearchRootDse(request, baseDn, rootDseFacts);
        }

        return FindEntry(baseDn) is { } baseEntry
            ? SearchBelow(request, baseEntry)
            : new LdapSearchOutcome([], new LdapResult(LdapResultCode.NoSuchObject, FindNearestSuperior(baseDn)?.Dn.Text ?? string.Empty, string.Empty));
    }

    private static LdapAttribute Attribute(string description, params IEnumerable<string> values) =>
        new(description, values.Select(Encoding.UTF8.GetBytes).ToArray());

    private static long SizeOf(LdapEntry entry) =>
        Encoding.UTF8.GetByteCount(entry.Dn.Text)
        + entry.Attributes.Sum(attribute => Encoding.UTF8.GetByteCount(attribute.Description.Text) + attribute.Values.Sum(value => (long)value.Length));

    private static LdapSearchOutcome Success(IReadOnlyList<LdapSearchResultEntry> entries) =>
        new(entries, new LdapResult(LdapResultCode.Success, string.Empty, string.Empty));

    private LdapDirectoryFault? FaultOf(LdapEntry entry, bool isWithinBounds)
    {
        if (entry.Attributes.Count == 0)
        {
            return LdapDirectoryFault.EntryWithoutAttribute;
        }

        if (entry.Dn.IsRoot)
        {
            return LdapDirectoryFault.RootDseEntry;
        }

        if (!isWithinBounds || HasAValuePastItsBound(entry))
        {
            return LdapDirectoryFault.PastBounds;
        }

        return entriesByNormalForm.ContainsKey(entry.Dn.NormalForm) ? LdapDirectoryFault.DuplicateDn : null;
    }

    private static bool HasAValuePastItsBound(LdapEntry entry) =>
        entry.Attributes.Any(attribute => attribute.Values.Any(value => value.Length > MaxValueBytes));

    private void ThrowOnAMissingParent()
    {
        for (var index = 0; index < Entries.Count; index++)
        {
            var superiors = Entries[index].Dn.SuperiorNormalForms().ToArray();
            if (superiors.Length > 0 && !entriesByNormalForm.ContainsKey(superiors[0]) && superiors.Any(entriesByNormalForm.ContainsKey))
            {
                throw new LdapDirectoryException(LdapDirectoryFault.ParentNotInDirectory, index);
            }
        }
    }

    private LdapEntry? FindNearestSuperior(LdapDistinguishedName dn) =>
        dn.SuperiorNormalForms()
            .Select(entriesByNormalForm.GetValueOrDefault)
            .FirstOrDefault(entry => entry is not null);

    private LdapSearchOutcome SearchRootDse(LdapSearchRequest request, LdapDistinguishedName rootDn, LdapRootDseFacts facts)
    {
        var operational = RootDseOperationalAttributes(facts);
        var isReturned = request.Scope == LdapSearchScope.BaseObject
            && LdapFilterEvaluator.Evaluate(request.Filter, rootDn, [.. RootDseUserAttributes, .. operational]) == LdapFilterResult.True;
        return Success(isReturned
            ? [new LdapSearchResultEntry(string.Empty, LdapAttributeSelection.Select(RootDseUserAttributes, operational, request.Attributes, request.TypesOnly))]
            : []);
    }

    private List<LdapAttribute> RootDseOperationalAttributes(LdapRootDseFacts facts)
    {
        var attributes = new List<LdapAttribute>();
        if (NamingContexts.Count > 0)
        {
            attributes.Add(Attribute("namingContexts", NamingContexts));
        }

        attributes.Add(Attribute("supportedLDAPVersion", "3"));
        if (facts.SaslMechanisms.Count > 0)
        {
            attributes.Add(Attribute("supportedSASLMechanisms", facts.SaslMechanisms));
        }

        if (facts.IsStartTlsOffered)
        {
            attributes.Add(Attribute("supportedExtension", RootDseExtensionStartTls));
        }

        return attributes;
    }

    private LdapSearchOutcome SearchBelow(LdapSearchRequest request, LdapEntry baseEntry)
    {
        var limit = SizeLimitOf(request);
        var deadline = DeadlineOf(request);
        var found = new List<LdapSearchResultEntry>();
        foreach (var entry in CandidatesOf(request.Scope, baseEntry))
        {
            if (timeProvider.GetUtcNow() >= deadline)
            {
                return new LdapSearchOutcome(found, new LdapResult(LdapResultCode.TimeLimitExceeded, string.Empty, string.Empty));
            }

            if (LdapFilterEvaluator.Evaluate(request.Filter, entry.Dn, entry.Attributes) != LdapFilterResult.True)
            {
                continue;
            }

            if (found.Count == limit)
            {
                return new LdapSearchOutcome(found, new LdapResult(LdapResultCode.SizeLimitExceeded, string.Empty, string.Empty));
            }

            found.Add(new LdapSearchResultEntry(entry.Dn.Text, LdapAttributeSelection.Select(entry.Attributes, [], request.Attributes, request.TypesOnly)));
        }

        return Success(found);
    }

    private int SizeLimitOf(LdapSearchRequest request) =>
        request.SizeLimit > 0 ? Math.Min(request.SizeLimit, maxSearchEntries) : maxSearchEntries;

    private DateTimeOffset DeadlineOf(LdapSearchRequest request) =>
        request.TimeLimit > 0 ? timeProvider.GetUtcNow().AddSeconds(request.TimeLimit) : DateTimeOffset.MaxValue;

    private IEnumerable<LdapEntry> CandidatesOf(LdapSearchScope scope, LdapEntry baseEntry) => scope switch
    {
        LdapSearchScope.BaseObject => [baseEntry],
        LdapSearchScope.SingleLevel => Entries.Where(entry => entry.Dn.IsWithin(baseEntry.Dn, levelsBelow: 1)),
        _ => Entries.Where(entry => entry.Dn.IsWithin(baseEntry.Dn, levelsBelow: null)),
    };
}
