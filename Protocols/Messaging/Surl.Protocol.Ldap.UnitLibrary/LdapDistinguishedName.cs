using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// A DN parsed by RFC 4514, kept as written and in a normal form that makes equal DNs compare
/// equal (ADR-0072 decision 1): each type lower-cased, each value matched by its type's equality
/// rule, each RDN's AVAs sorted.
/// </summary>
internal sealed class LdapDistinguishedName
{
    private LdapDistinguishedName(string text, IReadOnlyList<IReadOnlyList<LdapAttributeValueAssertion>> relativeDistinguishedNames)
    {
        Text = text;
        RelativeDistinguishedNames = relativeDistinguishedNames;
        RelativeDistinguishedNameKeys = relativeDistinguishedNames.Select(NormalFormOf).ToArray();
        NormalForm = string.Join(',', RelativeDistinguishedNameKeys);
    }

    /// <summary>Gets the DN as written.</summary>
    public string Text { get; }

    /// <summary>Gets the RDNs, leftmost (the entry's own) first; empty for the root DSE's empty DN.</summary>
    public IReadOnlyList<IReadOnlyList<LdapAttributeValueAssertion>> RelativeDistinguishedNames { get; }

    /// <summary>Gets the normal form, equal for two DNs exactly when they name the same entry.</summary>
    public string NormalForm { get; }

    /// <summary>Gets a value indicating whether this is the empty DN, the root DSE's.</summary>
    public bool IsRoot => RelativeDistinguishedNames.Count == 0;

    private IReadOnlyList<string> RelativeDistinguishedNameKeys { get; }

    /// <summary>
    /// Parses a DN by RFC 4514, accepting spaces around the separators and <c>=</c>.
    /// </summary>
    /// <param name="text">The DN as written.</param>
    /// <param name="distinguishedName">The DN, when it parses.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is an RFC 4514 DN.</returns>
    public static bool TryParse(string text, [NotNullWhen(true)] out LdapDistinguishedName? distinguishedName)
    {
        var relativeDistinguishedNames = LdapDistinguishedNameParser.TryParse(text);
        distinguishedName = relativeDistinguishedNames is null ? null : new LdapDistinguishedName(text, relativeDistinguishedNames);
        return distinguishedName is not null;
    }

    /// <summary>
    /// The normal forms of this DN's superiors, nearest first: the parent's, its parent's, and so
    /// on up to a one-RDN DN.
    /// </summary>
    /// <returns>The superiors' normal forms.</returns>
    public IEnumerable<string> SuperiorNormalForms()
    {
        for (var skipped = 1; skipped < RelativeDistinguishedNameKeys.Count; skipped++)
        {
            yield return string.Join(',', RelativeDistinguishedNameKeys.Skip(skipped));
        }
    }

    /// <summary>
    /// Tells whether this DN is <paramref name="ancestor"/> or lies below it.
    /// </summary>
    /// <param name="ancestor">The DN that may hold this one.</param>
    /// <param name="levelsBelow">How many RDNs longer this DN must be; <see langword="null"/> for any number, none included.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public bool IsWithin(LdapDistinguishedName ancestor, int? levelsBelow)
    {
        var extra = RelativeDistinguishedNameKeys.Count - ancestor.RelativeDistinguishedNameKeys.Count;
        return extra >= 0
            && (levelsBelow is null || extra == levelsBelow)
            && RelativeDistinguishedNameKeys.Skip(extra).SequenceEqual(ancestor.RelativeDistinguishedNameKeys, StringComparer.Ordinal);
    }

    private static string NormalFormOf(IReadOnlyList<LdapAttributeValueAssertion> relativeDistinguishedName) =>
        string.Join('+', relativeDistinguishedName.Select(NormalFormOf).Order(StringComparer.Ordinal));

    private static string NormalFormOf(LdapAttributeValueAssertion assertion)
    {
        var type = assertion.Type.ToLowerInvariant();
        if (assertion.IsBerEncoded)
        {
            return $"{type}=#{Convert.ToHexString(assertion.Value)}";
        }

        var rule = LdapMatchingRules.ForDescription(LdapAttributeDescription.Parse(assertion.Type));
        return $"{type}={Convert.ToHexString(Encoding.UTF8.GetBytes(LdapMatchingRules.NormalForm(rule, assertion.Value)))}";
    }
}
