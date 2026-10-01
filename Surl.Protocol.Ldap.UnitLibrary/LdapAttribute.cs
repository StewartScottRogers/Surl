namespace Surl.Protocol.Ldap;

/// <summary>
/// One attribute of a directory entry (ADR-0072 decision 1): a description and its values, each
/// an octet string, in the order the directory's source gave them.
/// </summary>
internal sealed class LdapAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LdapAttribute"/> class.
    /// </summary>
    /// <param name="description">The attribute description as written, such as <c>cn</c> or <c>userCertificate;binary</c>.</param>
    /// <param name="values">The values, in order.</param>
    public LdapAttribute(string description, IReadOnlyList<byte[]> values)
    {
        Description = LdapAttributeDescription.Parse(description);
        Values = values;
    }

    /// <summary>Gets the attribute description.</summary>
    public LdapAttributeDescription Description { get; }

    /// <summary>Gets the values, in order.</summary>
    public IReadOnlyList<byte[]> Values { get; }
}
