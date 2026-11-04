namespace Surl.Protocol.Ldap;

/// <summary>
/// An attribute description (RFC 4512, section 2.5): a type and its options, such as
/// <c>userCertificate;binary</c>. Types compare case-insensitively; options are kept lower-cased.
/// </summary>
internal sealed class LdapAttributeDescription
{
    private LdapAttributeDescription(string text, string type, IReadOnlyList<string> options)
    {
        Text = text;
        Type = type;
        Options = options;
    }

    /// <summary>Gets the description as written.</summary>
    public string Text { get; }

    /// <summary>Gets the attribute type, the part before the first <c>;</c>.</summary>
    public string Type { get; }

    /// <summary>Gets the options, lower-cased, in the order written.</summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>
    /// Splits a description into its type and options.
    /// </summary>
    /// <param name="text">The description as written.</param>
    /// <returns>The description.</returns>
    public static LdapAttributeDescription Parse(string text)
    {
        var parts = text.Split(';');
        return new LdapAttributeDescription(text, parts[0], parts[1..].Select(option => option.ToLowerInvariant()).ToArray());
    }

    /// <summary>
    /// Tells whether the description carries an option, given lower-cased.
    /// </summary>
    /// <param name="option">The option, lower-cased.</param>
    /// <returns><see langword="true"/> when the description carries it.</returns>
    public bool HasOption(string option) => Options.Contains(option, StringComparer.Ordinal);

    /// <summary>
    /// Tells whether this description, named in a filter or an attribute selection, names
    /// <paramref name="attribute"/>: the same type, and every option of this one among its options
    /// (RFC 4512, section 2.5.2), so <c>cn</c> names <c>cn;lang-en</c> too.
    /// </summary>
    /// <param name="attribute">An entry's attribute description.</param>
    /// <returns><see langword="true"/> when this description names it.</returns>
    public bool Names(LdapAttributeDescription attribute) =>
        string.Equals(Type, attribute.Type, StringComparison.OrdinalIgnoreCase)
        && Options.All(attribute.HasOption);
}
