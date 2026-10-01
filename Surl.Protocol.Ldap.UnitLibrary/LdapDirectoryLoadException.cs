namespace Surl.Protocol.Ldap;

/// <summary>
/// Thrown by <see cref="LdapProtocolServer.LoadAsync"/> when the directory's file cannot be
/// loaded (ADR-0072 decision 1): it cannot be read, or it is not LDIF content the directory can
/// hold. Nothing is served. The composition root answers it with <c>CouldNotReadFile</c> (37) and
/// <c>surl: (37) Could not read &lt;FilePath&gt;: &lt;Message&gt;</c>.
/// </summary>
public sealed class LdapDirectoryLoadException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="filePath">The full path of the directory's file.</param>
    /// <param name="reason">Why it cannot be loaded, the exception's message: the read failure's
    /// message, or <c>line &lt;n&gt;: &lt;what&gt;</c>.</param>
    /// <param name="innerException">The exception behind it.</param>
    public LdapDirectoryLoadException(string filePath, string reason, Exception innerException)
        : base(reason, innerException)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// The full path of the directory's file.
    /// </summary>
    public string FilePath { get; }
}
