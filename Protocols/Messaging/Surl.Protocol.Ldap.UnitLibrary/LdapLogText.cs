using System.Globalization;
using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Renders what the verbose log says about an LDAP exchange: a peer's text escaped as ADR-0006
/// section 3 requires, and a result code by its RFC 4511 name.
/// </summary>
internal static class LdapLogText
{
    /// <summary>
    /// Renders printable ASCII (<c>0x20</c> to <c>0x7E</c>) other than backslash as itself, and
    /// every other byte of <paramref name="text"/>'s UTF-8 as <c>\xHH</c>.
    /// </summary>
    /// <param name="text">The peer's text, such as a base DN or a bind name.</param>
    /// <returns>The rendering, reversible and free of control characters.</returns>
    public static string Render(string text)
    {
        var rendered = new StringBuilder(text.Length);
        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            rendered.Append(value is >= 0x20 and < 0x7F and not (byte)'\\'
                ? ((char)value).ToString()
                : @"\x" + value.ToString("X2", CultureInfo.InvariantCulture));
        }

        return rendered.ToString();
    }

    /// <summary>
    /// The result code's name as RFC 4511 section 4.1.9 writes it, e.g.
    /// <c>confidentialityRequired</c>.
    /// </summary>
    /// <param name="code">The result code.</param>
    /// <returns>Its name, starting lower case.</returns>
    public static string NameOf(LdapResultCode code)
    {
        var name = code.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
