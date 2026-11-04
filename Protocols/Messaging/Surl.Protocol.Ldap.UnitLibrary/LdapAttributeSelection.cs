namespace Surl.Protocol.Ldap;

/// <summary>
/// The attributes a search returns of an entry (RFC 4511, section 4.5.1.8; ADR-0072 decision 1):
/// none or <c>*</c>, every user attribute; <c>1.1</c> alone, none; <c>+</c>, every operational
/// attribute; a name, the attributes it names, case-insensitively. A name the entry lacks is
/// skipped. With types only, each selected attribute goes with no values.
/// </summary>
internal static class LdapAttributeSelection
{
    private const string AllUserAttributes = "*";
    private const string AllOperationalAttributes = "+";
    private const string NoAttributes = "1.1";

    /// <summary>
    /// Selects the attributes to return, user attributes first, each in the entry's order.
    /// </summary>
    /// <param name="userAttributes">The entry's user attributes.</param>
    /// <param name="operationalAttributes">The entry's operational attributes; only the root DSE has any.</param>
    /// <param name="requested">The search's attribute selection, as sent.</param>
    /// <param name="typesOnly">Whether only descriptions, with no values, are returned.</param>
    /// <returns>The attributes to send.</returns>
    public static IReadOnlyList<LdapPartialAttribute> Select(
        IReadOnlyList<LdapAttribute> userAttributes,
        IReadOnlyList<LdapAttribute> operationalAttributes,
        IReadOnlyList<string> requested,
        bool typesOnly)
    {
        var allUser = requested.Count == 0 || requested.Contains(AllUserAttributes, StringComparer.Ordinal);
        var allOperational = requested.Contains(AllOperationalAttributes, StringComparer.Ordinal);
        var named = requested
            .Where(name => name is not (AllUserAttributes or AllOperationalAttributes or NoAttributes))
            .Select(LdapAttributeDescription.Parse)
            .ToArray();

        return userAttributes.Where(attribute => allUser || IsNamed(attribute, named))
            .Concat(operationalAttributes.Where(attribute => allOperational || IsNamed(attribute, named)))
            .Select(attribute => new LdapPartialAttribute(attribute.Description.Text, typesOnly ? [] : attribute.Values))
            .ToArray();
    }

    private static bool IsNamed(LdapAttribute attribute, IReadOnlyList<LdapAttributeDescription> named) =>
        named.Any(name => name.Names(attribute.Description));
}
