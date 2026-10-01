namespace Surl.Protocol.Ssh;

/// <summary>
/// An <c>ATTRS</c> field a client sent (draft-ietf-secsh-filexfer-02, section 5), with what the
/// write side acts on: the size, the permissions (only ever noted) and the modification time. The
/// owners and extended pairs are read past; only their presence matters (ADR-0054, decision 10).
/// </summary>
/// <param name="Flags">The <c>flags</c> word as sent.</param>
/// <param name="Size">The <c>size</c> when <c>SIZE</c> is set.</param>
/// <param name="Permissions">The <c>permissions</c> when <c>PERMISSIONS</c> is set.</param>
/// <param name="ModificationTime">The <c>mtime</c>, in Unix seconds, when <c>ACMODTIME</c> is set.</param>
internal sealed record SftpAttributes(uint Flags, ulong? Size, uint? Permissions, uint? ModificationTime)
{
    /// <summary><c>SSH_FILEXFER_ATTR_SIZE</c>.</summary>
    public const uint SizeFlag = 0x00000001;

    /// <summary><c>SSH_FILEXFER_ATTR_UIDGID</c>.</summary>
    public const uint OwnersFlag = 0x00000002;

    /// <summary><c>SSH_FILEXFER_ATTR_PERMISSIONS</c>.</summary>
    public const uint PermissionsFlag = 0x00000004;

    /// <summary><c>SSH_FILEXFER_ATTR_ACMODTIME</c>.</summary>
    public const uint TimesFlag = 0x00000008;

    /// <summary><c>SSH_FILEXFER_ATTR_EXTENDED</c>.</summary>
    public const uint ExtendedFlag = 0x80000000;

    /// <summary>
    /// Whether the attributes ask for something the store cannot keep: owners, permissions or an
    /// extended pair (decision 10: <c>OP_UNSUPPORTED</c> <c>Permissions and owners are not kept</c>).
    /// </summary>
    public bool AsksForWhatIsNotKept => (Flags & (OwnersFlag | PermissionsFlag | ExtendedFlag)) != 0;
}
