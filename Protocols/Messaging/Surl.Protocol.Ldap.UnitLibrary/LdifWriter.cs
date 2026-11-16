using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Writes directory entries as LDIF content records (RFC 2849) for a test reading the directory back
/// (ADR-0164 decision 9). A <c>userPassword</c> attribute, with any options, is never written. What it writes,
/// <see cref="LdifReader"/> reads back as the same entries less those attributes.
/// </summary>
internal static class LdifWriter
{
    private const int LineLength = 76;

    /// <summary>The attribute type whose values are secrets and are omitted.</summary>
    public const string OmittedAttributeType = "userPassword";

    /// <summary>
    /// Writes entries as <c>version: 1</c>, then one record per entry in the order given, each after a blank line.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The LDIF text, lines ending in LF; <c>version: 1</c> alone when there are no entries.</returns>
    public static string Write(IReadOnlyList<LdapEntry> entries)
    {
        var text = new StringBuilder("version: 1\n");
        foreach (var entry in entries)
        {
            text.Append('\n');
            AppendLine(text, "dn", Encoding.UTF8.GetBytes(entry.Dn.Text));
            foreach (var attribute in entry.Attributes.Where(IsKept))
            {
                foreach (var value in attribute.Values)
                {
                    AppendLine(text, attribute.Description.Text, value);
                }
            }
        }

        return text.ToString();
    }

    private static bool IsKept(LdapAttribute attribute) =>
        !string.Equals(attribute.Description.Type, OmittedAttributeType, StringComparison.OrdinalIgnoreCase);

    private static void AppendLine(StringBuilder text, string description, byte[] value)
    {
        var line = IsSafeString(value)
            ? description + ": " + Encoding.ASCII.GetString(value)
            : description + ":: " + Convert.ToBase64String(value);
        text.Append(line, 0, Math.Min(line.Length, LineLength)).Append('\n');
        for (var start = LineLength; start < line.Length; start += LineLength - 1)
        {
            text.Append(' ').Append(line, start, Math.Min(LineLength - 1, line.Length - start)).Append('\n');
        }
    }

    // RFC 2849 SAFE-STRING: ASCII without NUL, LF or CR, not starting with a space, ':' or '<', not ending in a space.
    private static bool IsSafeString(byte[] value) =>
        value.All(octet => octet is > 0 and < 128 and not (byte)'\n' and not (byte)'\r')
        && (value.Length == 0 || (value[0] is not (byte)' ' and not (byte)':' and not (byte)'<' && value[^1] != (byte)' '));
}
