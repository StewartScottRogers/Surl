namespace Surl.Protocol.Ldap;

/// <summary>
/// Why a list of entries cannot be a directory (ADR-0072 decision 1).
/// </summary>
internal enum LdapDirectoryFault
{
    /// <summary>An entry has no attribute.</summary>
    EntryWithoutAttribute,

    /// <summary>An entry is named by the empty DN, which is the root DSE's: computed, never stored.</summary>
    RootDseEntry,

    /// <summary>An entry's DN equals, in normal form, an earlier entry's.</summary>
    DuplicateDn,

    /// <summary>An entry goes past one of the directory's bounds: entries, total bytes, or the bytes of one value.</summary>
    PastBounds,

    /// <summary>An entry's parent is not in the directory although another of its superiors is.</summary>
    ParentNotInDirectory,
}
