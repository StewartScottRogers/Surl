namespace Surl.Protocol.Ldap;

/// <summary>
/// The entries a directory held when it was snapshotted: opaque, in memory, and good only for the
/// directory that took it (ADR-0158 decision 5).
/// </summary>
internal sealed class LdapDirectorySnapshot
{
    internal LdapDirectorySnapshot(LdapDirectory owner, LdapDirectoryContents contents)
    {
        Owner = owner;
        Contents = contents;
    }

    /// <summary>Gets how many bytes of DNs, descriptions and values the snapshot holds.</summary>
    public long ByteCount => Contents.ByteCount;

    internal LdapDirectory Owner { get; }

    internal LdapDirectoryContents Contents { get; }
}
