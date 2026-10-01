namespace Surl.Protocol.Ldap;

/// <summary>
/// Thrown when an LDIF file cannot be the directory (ADR-0072 decision 1); its message is
/// <c>line &lt;n&gt;: &lt;what&gt;</c>, the reason <see cref="LdapDirectoryLoadException"/> carries.
/// </summary>
internal sealed class LdifFormatException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LdifFormatException"/> class.
    /// </summary>
    /// <param name="line">The one-based number of the first physical line of the fault.</param>
    /// <param name="what">What is wrong, one of <see cref="LdifFaultText"/>'s texts.</param>
    public LdifFormatException(int line, string what)
        : base($"line {line}: {what}")
    {
        Line = line;
    }

    /// <summary>Gets the one-based number of the first physical line of the fault.</summary>
    public int Line { get; }
}
