using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Builds directory entries, filters and searches for the directory's tests.
/// </summary>
internal static class LdapDirectoryFixture
{
    /// <summary>A root DSE with nothing offered.</summary>
    public static readonly LdapRootDseFacts NothingOffered = new([], IsStartTlsOffered: false);

    /// <summary>The filter <c>(objectClass=*)</c>.</summary>
    public static readonly LdapFilter AnyObject = new LdapPresentFilter("objectClass");

    /// <summary>
    /// The fixture directory: <c>dc=example,dc=com</c>, two people, an organisational unit with a
    /// person under it, and a second naming context.
    /// </summary>
    /// <returns>The entries.</returns>
    public static IReadOnlyList<LdapEntry> PeopleEntries() =>
    [
        Entry("dc=example,dc=com", ("objectClass", "domain"), ("dc", "example")),
        Entry(
            "cn=alice,dc=example,dc=com",
            ("objectClass", "person"),
            ("cn", "alice"),
            ("sn", "Smith"),
            ("mail", "alice@example.com"),
            ("uidNumber", "1000"),
            ("cn;lang-fr", "alicia")),
        Entry(
            "cn=bob,dc=example,dc=com",
            ("objectClass", "person"),
            ("cn", "bob"),
            ("sn", "Jones"),
            ("mail", "bob@other.example"),
            ("uidNumber", "20"),
            ("description", "café")),
        Entry("ou=staff,dc=example,dc=com", ("objectClass", "organizationalUnit"), ("ou", "staff")),
        Entry("uid=carol,ou=staff,dc=example,dc=com", ("objectClass", "person"), ("uid", "carol"), ("cn", "Carol  Ann")),
        Entry("o=other", ("objectClass", "organization"), ("o", "other")),
    ];

    /// <summary>
    /// Builds an entry from a DN and description-value pairs; pairs sharing a description become one
    /// attribute.
    /// </summary>
    /// <param name="dn">The DN.</param>
    /// <param name="values">The description-value pairs.</param>
    /// <returns>The entry.</returns>
    public static LdapEntry Entry(string dn, params (string Description, string Value)[] values) =>
        new(
            Dn(dn),
            values.GroupBy(pair => pair.Description, StringComparer.Ordinal)
                .Select(group => new LdapAttribute(group.Key, group.Select(pair => Encoding.UTF8.GetBytes(pair.Value)).ToArray()))
                .ToArray());

    /// <summary>
    /// Parses a DN the test expects to parse.
    /// </summary>
    /// <param name="text">The DN.</param>
    /// <returns>The DN.</returns>
    public static LdapDistinguishedName Dn(string text)
    {
        Assert.IsTrue(LdapDistinguishedName.TryParse(text, out var dn), text);
        return dn;
    }

    /// <summary>
    /// Builds the fixture directory on the system clock.
    /// </summary>
    /// <returns>The directory.</returns>
    public static LdapDirectory PeopleDirectory() => new(PeopleEntries(), TimeProvider.System);

    /// <summary>
    /// Builds a search request.
    /// </summary>
    /// <param name="baseObject">The base DN.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="filter">The filter; <see langword="null"/> for <c>(objectClass=*)</c>.</param>
    /// <param name="attributes">The attribute selection.</param>
    /// <returns>The request.</returns>
    public static LdapSearchRequest Search(string baseObject, LdapSearchScope scope, LdapFilter? filter = null, params string[] attributes) =>
        new(baseObject, scope, LdapDerefAliases.NeverDerefAliases, SizeLimit: 0, TimeLimit: 0, TypesOnly: false, filter ?? AnyObject, attributes);

    /// <summary>
    /// Searches a directory on a connection that offers nothing.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="request">The search.</param>
    /// <returns>The outcome.</returns>
    public static LdapSearchOutcome Search(this LdapDirectory directory, LdapSearchRequest request) => directory.Search(request, NothingOffered);

    /// <summary>
    /// The DNs of a search's entries.
    /// </summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The DNs, joined by <c>|</c>.</returns>
    public static string DnsOf(LdapSearchOutcome outcome) => string.Join('|', outcome.Entries.Select(entry => entry.ObjectName));

    /// <summary>
    /// Writes an entry's returned attributes as <c>type=value,value;type2=...</c>.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The text.</returns>
    public static string AttributesOf(LdapSearchResultEntry entry) =>
        string.Join(';', entry.Attributes.Select(attribute => $"{attribute.Type}={string.Join(',', attribute.Values.Select(Encoding.UTF8.GetString))}"));

    /// <summary>The bytes of a UTF-8 string.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
