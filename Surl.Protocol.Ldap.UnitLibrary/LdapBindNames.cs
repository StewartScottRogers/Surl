using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Maps a simple bind's name to the account name its password is checked against (ADR-0072
/// decision 2).
/// </summary>
internal static class LdapBindNames
{
    /// <summary>
    /// The account a bind name names: a name with no unescaped <c>=</c> is the account name as
    /// sent (curl's <c>-u alice:secret</c>); a DN whose leftmost RDN is one <c>uid</c> or
    /// <c>cn</c> AVA is that AVA's value (<c>cn=alice,dc=example,dc=com</c> is <c>alice</c>);
    /// any other name is returned whole, an account no one has, so its check fails as a wrong
    /// password does, after the same delay.
    /// </summary>
    /// <param name="bindName">The bind's <c>name</c>, as sent; not empty.</param>
    /// <returns>The account name to check.</returns>
    public static string AccountNameOf(string bindName)
    {
        return HasUnescapedEqualsSign(bindName)
            && LdapDistinguishedName.TryParse(bindName, out var dn)
            && LeftmostAccountValueOf(dn) is { } accountName
                ? accountName
                : bindName;
    }

    // The value of the DN's leftmost RDN when it is one uid or cn AVA written as a string.
    private static string? LeftmostAccountValueOf(LdapDistinguishedName dn)
    {
        var leftmost = dn.RelativeDistinguishedNames[0];
        return leftmost.Count == 1 && !leftmost[0].IsBerEncoded && IsAccountType(leftmost[0].Type)
            ? Encoding.UTF8.GetString(leftmost[0].Value)
            : null;
    }

    private static bool IsAccountType(string type) =>
        string.Equals(type, "uid", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "cn", StringComparison.OrdinalIgnoreCase);

    private static bool HasUnescapedEqualsSign(string name)
    {
        for (var index = 0; index < name.Length; index++)
        {
            if (name[index] == '\\')
            {
                index++;
            }
            else if (name[index] == '=')
            {
                return true;
            }
        }

        return false;
    }
}
