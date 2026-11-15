using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// One immutable set of directory entries, validated against the directory's bounds and indexed by
/// normal form. A directory swaps whole contents, so a search sees the old set or the new one, never a mix.
/// </summary>
internal sealed class LdapDirectoryContents
{
    private readonly Dictionary<string, LdapEntry> entriesByNormalForm = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="LdapDirectoryContents"/> class.
    /// </summary>
    /// <param name="entries">The entries, in their source's order.</param>
    /// <param name="maxEntries">The most entries the contents hold.</param>
    /// <param name="maxTotalBytes">The most bytes of DNs, descriptions and values the contents hold.</param>
    /// <exception cref="LdapDirectoryException">An entry cannot be in a directory.</exception>
    public LdapDirectoryContents(IReadOnlyList<LdapEntry> entries, int maxEntries, long maxTotalBytes)
    {
        Entries = entries;
        for (var index = 0; index < entries.Count; index++)
        {
            ByteCount += SizeOf(entries[index]);
            var fault = FaultOf(entries[index], index < maxEntries && ByteCount <= maxTotalBytes);
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

    /// <summary>Gets the DNs, as written and in ordinal order ignoring case, of the entries with no superior.</summary>
    public IReadOnlyList<string> NamingContexts { get; }

    /// <summary>Gets the bytes of DNs, descriptions and values the entries hold.</summary>
    public long ByteCount { get; private set; }

    /// <summary>
    /// Measures an entry as the directory's byte bound counts it.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The bytes of its DN, attribute descriptions and values.</returns>
    public static long SizeOf(LdapEntry entry) =>
        Encoding.UTF8.GetByteCount(entry.Dn.Text)
        + entry.Attributes.Sum(attribute => Encoding.UTF8.GetByteCount(attribute.Description.Text) + attribute.Values.Sum(value => (long)value.Length));

    /// <summary>
    /// Finds the entry a DN names, in normal form.
    /// </summary>
    /// <param name="dn">The DN.</param>
    /// <returns>The entry; <see langword="null"/> when there is none.</returns>
    public LdapEntry? FindEntry(LdapDistinguishedName dn) => entriesByNormalForm.GetValueOrDefault(dn.NormalForm);

    /// <summary>
    /// Finds the nearest existing superior of a DN.
    /// </summary>
    /// <param name="dn">The DN.</param>
    /// <returns>The superior; <see langword="null"/> when none exists.</returns>
    public LdapEntry? FindNearestSuperior(LdapDistinguishedName dn) =>
        dn.SuperiorNormalForms()
            .Select(entriesByNormalForm.GetValueOrDefault)
            .FirstOrDefault(entry => entry is not null);

    private static bool HasAValuePastItsBound(LdapEntry entry) =>
        entry.Attributes.Any(attribute => attribute.Values.Any(value => value.Length > LdapDirectory.MaxValueBytes));

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
}
