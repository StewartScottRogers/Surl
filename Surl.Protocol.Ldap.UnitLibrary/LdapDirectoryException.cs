namespace Surl.Protocol.Ldap;

/// <summary>
/// Thrown when a list of entries cannot be a directory; names the fault and the entry's index so
/// the directory's loader can name the record's line.
/// </summary>
internal sealed class LdapDirectoryException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LdapDirectoryException"/> class.
    /// </summary>
    /// <param name="fault">Why the entries cannot be a directory.</param>
    /// <param name="entryIndex">The zero-based index of the first entry at fault.</param>
    public LdapDirectoryException(LdapDirectoryFault fault, int entryIndex)
        : base($"Entry {entryIndex}: {fault}.")
    {
        Fault = fault;
        EntryIndex = entryIndex;
    }

    /// <summary>Gets why the entries cannot be a directory.</summary>
    public LdapDirectoryFault Fault { get; }

    /// <summary>Gets the zero-based index of the first entry at fault.</summary>
    public int EntryIndex { get; }
}
