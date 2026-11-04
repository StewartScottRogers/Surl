using System.Globalization;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// ADR-0072 decision 10's directory, as the LDAP conformance tests serve it from a temporary
/// <c>--directory</c>: the base <c>dc=example,dc=com</c>, the people <c>alice</c> and <c>bob</c>,
/// and 10001 entries under <c>ou=many</c>, one past the search bound, written to
/// <c>.surl/ldap/directory.ldif</c>.
/// </summary>
internal static class LdapConformanceDirectory
{
    /// <summary>The account <c>alice:secret</c>, for surl's <c>--user</c> and curl's <c>-u</c>.</summary>
    public const string Account = "alice:secret";

    /// <summary>The account with a wrong password.</summary>
    public const string WrongPassword = "alice:wrong";

    /// <summary>The search base every case reads.</summary>
    public const string BaseDn = "dc=example,dc=com";

    private const int ManyEntries = 10001;

    /// <summary>The directory's files, by path relative to the served directory.</summary>
    public static IReadOnlyDictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>
    {
        [Path.Combine(".surl", "ldap", "directory.ldif")] = Encoding.UTF8.GetBytes(Ldif()),
    };

    /// <summary>The subdirectories <see cref="Files"/> needs, parents first.</summary>
    public static IReadOnlyList<string> Subdirectories { get; } = [".surl", Path.Combine(".surl", "ldap")];

    private static string Ldif()
    {
        var ldif = new StringBuilder()
            .Append("version: 1\n\n")
            .Append("dn: dc=example,dc=com\nobjectClass: domain\n\n")
            .Append("dn: cn=alice,dc=example,dc=com\nobjectClass: person\ncn: alice\nsn: Smith\nmail: alice@example.com\n\n")
            .Append("dn: cn=bob,dc=example,dc=com\nobjectClass: person\ncn: bob\nsn: Jones\nmail: bob@other.example\n")
            .Append("description:: Y2Fmw6k=\n\n")
            .Append("dn: ou=many,dc=example,dc=com\nobjectClass: organizationalUnit\nou: many\n\n");
        for (var entry = 0; entry < ManyEntries; entry++)
        {
            ldif.Append(CultureInfo.InvariantCulture, $"dn: cn=entry{entry},ou=many,dc=example,dc=com\nobjectClass: person\ncn: entry{entry}\n\n");
        }

        return ldif.ToString();
    }
}
