using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ldap;

/// <summary>
/// The directory the LDAP server serves (ADR-0072 decision 1): entries in their source's order,
/// replaced whole when seeded, reset or restored (so a search sees the old set or the new one), bounded, and searched with base,
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

    /// <summary>The most bytes of LDIF text one seed holds (16 MiB, ADR-0158 decision 3).</summary>
    public const int MaxSeedBytes = 16777216;

    private const string RootDseExtensionStartTls = "1.3.6.1.4.1.1466.20037";

    private static readonly IReadOnlyList<LdapAttribute> RootDseUserAttributes = [Attribute("objectClass", "top")];

    private readonly TimeProvider timeProvider;
    private readonly int maxSearchEntries;
    private readonly int maxEntries;
    private readonly long maxTotalBytes;
    private readonly ISurlEventPublisher events;
    private readonly Lock changeLock = new();
    private LdapDirectoryContents configured;
    private volatile LdapDirectoryContents contents;

    /// <summary>
    /// Initializes a new instance of the <see cref="LdapDirectory"/> class.
    /// </summary>
    /// <param name="entries">The entries, in their source's order; <see cref="Reset"/> returns to them and every kept seed.</param>
    /// <param name="timeProvider">The clock a search's <c>timeLimit</c> is measured by.</param>
    /// <param name="maxEntries">The most entries the directory holds.</param>
    /// <param name="maxTotalBytes">The most bytes of DNs, descriptions and values the directory holds.</param>
    /// <param name="maxSearchEntries">The most entries one search returns.</param>
    /// <param name="events">Where seeds, resets and restores are published; nowhere when <see langword="null"/>.</param>
    /// <exception cref="LdapDirectoryException">An entry cannot be in a directory.</exception>
    public LdapDirectory(
        IReadOnlyList<LdapEntry> entries,
        TimeProvider timeProvider,
        int maxEntries = DefaultMaxEntries,
        long maxTotalBytes = DefaultMaxTotalBytes,
        int maxSearchEntries = DefaultMaxSearchEntries,
        ISurlEventPublisher? events = null)
    {
        this.timeProvider = timeProvider;
        this.maxSearchEntries = maxSearchEntries;
        this.maxEntries = maxEntries;
        this.maxTotalBytes = maxTotalBytes;
        this.events = events ?? DiscardingSurlEventPublisher.Instance;
        configured = new LdapDirectoryContents(entries, maxEntries, maxTotalBytes);
        contents = configured;
    }

    /// <summary>Gets the entries, in their source's order.</summary>
    public IReadOnlyList<LdapEntry> Entries => contents.Entries;

    /// <summary>Gets the DNs, as written and in ordinal order ignoring case, of the entries with no superior in the directory.</summary>
    public IReadOnlyList<string> NamingContexts => contents.NamingContexts;

    /// <summary>
    /// Finds the entry a DN names, in normal form.
    /// </summary>
    /// <param name="dn">The DN.</param>
    /// <returns>The entry; <see langword="null"/> when the directory has none.</returns>
    public LdapEntry? FindEntry(LdapDistinguishedName dn) => contents.FindEntry(dn);

    /// <summary>
    /// Adds the content records of LDIF text (ADR-0158 decision 3), all or none, and publishes
    /// <see cref="StoreChange.Seeded"/> for each. It never consults the fault plan. A seed is lost by
    /// <see cref="Reset"/> unless <paramref name="keepOnReset"/> is set (ADR-0158 decision 4).
    /// </summary>
    /// <param name="ldif">LDIF content records, at most <see cref="MaxSeedBytes"/> bytes of UTF-8.</param>
    /// <param name="keepOnReset">Whether <see cref="Reset"/> returns to a set holding these entries too; they must then be addable to that set too, and an entry with a parent among the live entries needs that parent in that set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ldif"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The text is not LDIF content the directory, or for a kept seed the set <see cref="Reset"/> returns to, can add.
    /// A refused record's message is <c>line &lt;n&gt;: &lt;what&gt;</c>. Neither set changes and nothing is published.
    /// </exception>
    public void SeedEntries(string ldif, bool keepOnReset = false)
    {
        ArgumentNullException.ThrowIfNull(ldif);
        var bytes = Encoding.UTF8.GetBytes(ldif);
        if (bytes.Length > MaxSeedBytes)
        {
            throw new InvalidOperationException($"the LDIF is past {MaxSeedBytes} bytes");
        }

        IReadOnlyList<LdifRecord> records;
        try
        {
            records = LdifReader.Read(bytes);
        }
        catch (LdifFormatException exception)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }

        lock (changeLock)
        {
            var added = records.Select(record => record.Entry).ToArray();
            var seeded = SeededContents(contents, records, added);
            configured = keepOnReset ? KeptContents(seeded, records, added) : configured;
            contents = seeded;
            foreach (var entry in added)
            {
                Publish(StoreChange.Seeded, entry.Dn.Text, LdapDirectoryContents.SizeOf(entry));
            }
        }
    }

    /// <summary>
    /// Writes the directory as LDIF content records for a test reading it back (ADR-0164 decision 9): publishes
    /// nothing, and omits every <c>userPassword</c> value.
    /// </summary>
    /// <returns>The LDIF text, <c>version: 1</c> then each entry in the directory's order.</returns>
    public string ExportLdif() => LdifWriter.Write(contents.Entries);

    /// <summary>
    /// Returns the directory to its configured entries - those it was built with, then every kept seed's - drops
    /// every other, and publishes <see cref="StoreChange.Reset"/>.
    /// </summary>
    public void Reset()
    {
        lock (changeLock)
        {
            contents = configured;
            Publish(StoreChange.Reset, string.Empty, 0);
        }
    }

    /// <summary>
    /// Copies the directory's entries.
    /// </summary>
    /// <returns>The snapshot, good for this directory only.</returns>
    public LdapDirectorySnapshot TakeSnapshot() => new(this, contents);

    /// <summary>
    /// Replaces the entries with <paramref name="snapshot"/>'s and publishes <see cref="StoreChange.Restored"/>.
    /// </summary>
    /// <param name="snapshot">A snapshot this directory took.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Another directory took the snapshot.</exception>
    public void Restore(LdapDirectorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!ReferenceEquals(snapshot.Owner, this))
        {
            throw new InvalidOperationException("the snapshot was taken from another directory");
        }

        lock (changeLock)
        {
            contents = snapshot.Contents;
            Publish(StoreChange.Restored, string.Empty, snapshot.ByteCount);
        }
    }

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
            return SearchRootDse(request, baseDn, rootDseFacts, contents);
        }

        var current = contents;
        return current.FindEntry(baseDn) is { } baseEntry
            ? SearchBelow(request, current, baseEntry)
            : new LdapSearchOutcome([], new LdapResult(LdapResultCode.NoSuchObject, current.FindNearestSuperior(baseDn)?.Dn.Text ?? string.Empty, string.Empty));
    }

    /// <summary>
    /// Answers a <c>CompareRequest</c> (RFC 4511, section 4.10; ADR-0072 decision 3) by the
    /// asserted type's equality rule: <c>compareTrue</c> or <c>compareFalse</c>,
    /// <c>undefinedAttributeType</c> for a type the entry lacks, <c>noSuchObject</c> with the
    /// nearest superior as matched DN for an entry that does not exist, and
    /// <c>invalidDNSyntax</c> for a DN that is not RFC 4514.
    /// </summary>
    /// <param name="request">The compare, as decoded.</param>
    /// <returns>The <c>CompareResponse</c>'s result.</returns>
    public LdapResult Compare(LdapCompareRequest request)
    {
        if (!LdapDistinguishedName.TryParse(request.Entry, out var dn))
        {
            return new LdapResult(LdapResultCode.InvalidDnSyntax, string.Empty, "the entry is not an RFC 4514 DN");
        }

        var current = contents;
        return current.FindEntry(dn) is { } entry
            ? CompareIn(entry, request)
            : new LdapResult(LdapResultCode.NoSuchObject, current.FindNearestSuperior(dn)?.Dn.Text ?? string.Empty, string.Empty);
    }

    private static LdapResult CompareIn(LdapEntry entry, LdapCompareRequest request)
    {
        if (LdapFilterEvaluator.Evaluate(new LdapPresentFilter(request.AttributeDescription), entry.Dn, entry.Attributes) != LdapFilterResult.True)
        {
            return new LdapResult(LdapResultCode.UndefinedAttributeType, string.Empty, string.Empty);
        }

        var equality = new LdapComparisonFilter(LdapComparison.EqualityMatch, request.AttributeDescription, request.AssertionValue);
        var isTrue = LdapFilterEvaluator.Evaluate(equality, entry.Dn, entry.Attributes) == LdapFilterResult.True;
        return new LdapResult(isTrue ? LdapResultCode.CompareTrue : LdapResultCode.CompareFalse, string.Empty, string.Empty);
    }

    private static LdapAttribute Attribute(string description, params IEnumerable<string> values) =>
        new(description, values.Select(Encoding.UTF8.GetBytes).ToArray());

    private static LdapSearchOutcome Success(IReadOnlyList<LdapSearchResultEntry> entries) =>
        new(entries, new LdapResult(LdapResultCode.Success, string.Empty, string.Empty));

    private LdapSearchOutcome SearchRootDse(LdapSearchRequest request, LdapDistinguishedName rootDn, LdapRootDseFacts facts, LdapDirectoryContents current)
    {
        var operational = RootDseOperationalAttributes(facts, current);
        var isReturned = request.Scope == LdapSearchScope.BaseObject
            && LdapFilterEvaluator.Evaluate(request.Filter, rootDn, [.. RootDseUserAttributes, .. operational]) == LdapFilterResult.True;
        return Success(isReturned
            ? [new LdapSearchResultEntry(string.Empty, LdapAttributeSelection.Select(RootDseUserAttributes, operational, request.Attributes, request.TypesOnly))]
            : []);
    }

    private List<LdapAttribute> RootDseOperationalAttributes(LdapRootDseFacts facts, LdapDirectoryContents current)
    {
        var attributes = new List<LdapAttribute>();
        if (current.NamingContexts.Count > 0)
        {
            attributes.Add(Attribute("namingContexts", current.NamingContexts));
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

    private LdapSearchOutcome SearchBelow(LdapSearchRequest request, LdapDirectoryContents current, LdapEntry baseEntry)
    {
        var limit = SizeLimitOf(request);
        var deadline = DeadlineOf(request);
        var found = new List<LdapSearchResultEntry>();
        foreach (var entry in CandidatesOf(request.Scope, current, baseEntry))
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

    private IEnumerable<LdapEntry> CandidatesOf(LdapSearchScope scope, LdapDirectoryContents current, LdapEntry baseEntry) => scope switch
    {
        LdapSearchScope.BaseObject => [baseEntry],
        LdapSearchScope.SingleLevel => current.Entries.Where(entry => entry.Dn.IsWithin(baseEntry.Dn, levelsBelow: 1)),
        _ => current.Entries.Where(entry => entry.Dn.IsWithin(baseEntry.Dn, levelsBelow: null)),
    };

    // A kept entry with no superior in the configured set is a naming context to the contents' own rules; it is
    // refused when it has a parent now, so that a reset never leaves behind a child of a dropped entry.
    private LdapDirectoryContents KeptContents(LdapDirectoryContents seeded, IReadOnlyList<LdifRecord> records, LdapEntry[] added)
    {
        var kept = SeededContents(configured, records, added);
        var orphan = Array.FindIndex(added, entry => seeded.FindNearestSuperior(entry.Dn) is not null && kept.FindNearestSuperior(entry.Dn) is null);
        return orphan < 0
            ? kept
            : throw new InvalidOperationException(
                new LdifFormatException(records[orphan].Line, LdifFaultText.Of(LdapDirectoryFault.ParentNotInDirectory)).Message);
    }

    private LdapDirectoryContents SeededContents(LdapDirectoryContents held, IReadOnlyList<LdifRecord> records, LdapEntry[] added)
    {
        var existing = held.Entries;
        try
        {
            return new LdapDirectoryContents([.. existing, .. added], maxEntries, maxTotalBytes);
        }
        catch (LdapDirectoryException exception)
        {
            var text = LdifFaultText.Of(exception.Fault);
            throw new InvalidOperationException(
                exception.EntryIndex >= existing.Count
                    ? new LdifFormatException(records[exception.EntryIndex - existing.Count].Line, text).Message
                    : $"the entry {existing[exception.EntryIndex].Dn.Text}: {text}",
                exception);
        }
    }

    private void Publish(StoreChange change, string name, long byteCount) =>
        events.Publish(new StoreChangedEvent(StoreKind.Directory, change, name, byteCount, ExchangeId: null));
}
