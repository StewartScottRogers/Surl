namespace Surl.Protocol.Ldap;

/// <summary>
/// What is wrong with an LDIF file that cannot be the directory, in ADR-0072 decision 1's words.
/// </summary>
internal static class LdifFaultText
{
    /// <summary>A line is not UTF-8, or the file starts with a byte order mark.</summary>
    public const string NotUtf8 = "not UTF-8";

    /// <summary>The <c>version</c> line names another version.</summary>
    public const string VersionNotOne = "version is not 1";

    /// <summary>A line starting with a space follows nothing it could continue.</summary>
    public const string ContinuationWithNothing = "a continuation line with nothing to continue";

    /// <summary>A record does not start with <c>dn</c>, or its <c>dn</c> is empty.</summary>
    public const string RecordWithoutDn = "a record without dn";

    /// <summary>A record's <c>dn</c> does not parse by RFC 4514.</summary>
    public const string DnNotRfc4514 = "a DN that is not RFC 4514";

    /// <summary>A line is not <c>&lt;description&gt;: &lt;value&gt;</c>.</summary>
    public const string NotDescriptionValue = "a line that is not <description>: <value>";

    /// <summary>A <c>::</c> value is not base64.</summary>
    public const string BadBase64 = "bad base64";

    /// <summary>A <c>:</c> value is not an RFC 2849 SAFE-STRING.</summary>
    public const string NotSafeString = "a value that is not a SAFE-STRING";

    /// <summary>A record carries <c>changetype</c>: it is a change record.</summary>
    public const string ChangeRecord = "a changetype record";

    /// <summary>A value is given by URL (<c>:&lt;</c>).</summary>
    public const string UrlValue = "a URL value";

    /// <summary>A record's DN equals an earlier record's.</summary>
    public const string DuplicateDn = "a duplicate DN";

    /// <summary>A record's parent is not in the file although another of its superiors is.</summary>
    public const string ParentNotInFile = "a parent DN that is not in the file";

    /// <summary>A record has a <c>dn</c> and nothing else.</summary>
    public const string EntryWithoutAttribute = "an entry with no attribute";

    /// <summary>A record goes past one of the directory's bounds.</summary>
    public const string PastBounds = "past the directory's bounds";

    private static readonly Dictionary<LdapDirectoryFault, string> DirectoryFaultTexts = new()
    {
        [LdapDirectoryFault.EntryWithoutAttribute] = EntryWithoutAttribute,
        [LdapDirectoryFault.RootDseEntry] = RecordWithoutDn,
        [LdapDirectoryFault.DuplicateDn] = DuplicateDn,
        [LdapDirectoryFault.PastBounds] = PastBounds,
        [LdapDirectoryFault.ParentNotInDirectory] = ParentNotInFile,
    };

    /// <summary>
    /// The text for a fault the directory found in the file's entries; an empty <c>dn</c>, the
    /// root DSE's, reads as a record without one.
    /// </summary>
    /// <param name="fault">The directory's fault.</param>
    /// <returns>The text.</returns>
    public static string Of(LdapDirectoryFault fault) => DirectoryFaultTexts[fault];
}
