namespace Surl.Protocol.Ssh;

/// <summary>
/// One SFTP request packet after its length: its type, its id, and its fields read front to back
/// (draft-ietf-secsh-filexfer-02, section 3). A field that runs past the packet, or bytes left
/// over once every field is read, make it a packet that does not parse, answered
/// <c>BAD_MESSAGE</c> (ADR-0054, decision 5).
/// </summary>
internal sealed class SftpRequest
{
    private readonly SshWireReader fields;
    private readonly int length;

    /// <summary>
    /// Reads the type and id of <paramref name="packet"/>.
    /// </summary>
    /// <param name="packet">The packet after its length field: at least 5 bytes, as the framing ensures.</param>
    public SftpRequest(byte[] packet)
    {
        Type = packet[0];
        fields = new SshWireReader(packet);
        length = packet.Length;
        fields.ReadByte();
        Id = fields.ReadUInt32();
    }

    /// <summary>
    /// The packet type.
    /// </summary>
    public byte Type { get; }

    /// <summary>
    /// The request id every reply carries.
    /// </summary>
    public uint Id { get; }

    /// <summary>
    /// The name the notes give the request.
    /// </summary>
    public string Name => SftpPacketType.NameOf(Type);

    /// <summary>
    /// Reads a <c>string</c> field: a path or a handle.
    /// </summary>
    /// <returns>The string's bytes.</returns>
    /// <exception cref="SshDisconnectRequiredException">The field runs past the packet.</exception>
    public ReadOnlyMemory<byte> ReadString() => fields.ReadString();

    /// <summary>
    /// Reads a <c>uint32</c> field.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="SshDisconnectRequiredException">The field runs past the packet.</exception>
    public uint ReadUInt32() => fields.ReadUInt32();

    /// <summary>
    /// Reads a <c>uint64</c> field.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="SshDisconnectRequiredException">The field runs past the packet.</exception>
    public ulong ReadUInt64() => fields.ReadUInt64();

    /// <summary>
    /// Reads an <c>ATTRS</c> field (draft-02, section 5): the size, the permissions and the
    /// modification time are kept, the owners, the access time and the extended pairs read past.
    /// </summary>
    /// <returns>The attributes.</returns>
    /// <exception cref="SshDisconnectRequiredException">A field runs past the packet.</exception>
    public SftpAttributes ReadAttributes()
    {
        var flags = fields.ReadUInt32();
        ulong? size = (flags & SftpAttributes.SizeFlag) != 0 ? fields.ReadUInt64() : null;
        fields.ReadBytes((flags & SftpAttributes.OwnersFlag) != 0 ? 8 : 0);
        uint? permissions = (flags & SftpAttributes.PermissionsFlag) != 0 ? fields.ReadUInt32() : null;
        var modificationTime = ReadModificationTime(flags);
        if ((flags & SftpAttributes.ExtendedFlag) != 0)
        {
            SkipExtendedAttributes();
        }

        return new SftpAttributes(flags, size, permissions, modificationTime);
    }

    // ACMODTIME: the access time, which the store does not keep, then the modification time.
    private uint? ReadModificationTime(uint flags)
    {
        if ((flags & SftpAttributes.TimesFlag) == 0)
        {
            return null;
        }

        fields.ReadUInt32();

        return fields.ReadUInt32();
    }

    private void SkipExtendedAttributes()
    {
        for (var count = fields.ReadUInt32(); count > 0; count--)
        {
            fields.ReadString();
            fields.ReadString();
        }
    }

    /// <summary>
    /// Checks that every byte of the packet was read.
    /// </summary>
    /// <exception cref="SshDisconnectRequiredException">Bytes are left over.</exception>
    public void RequireEnd()
    {
        if (fields.Position != length)
        {
            throw SshDisconnectRequiredException.ProtocolError("An SFTP request held bytes after its last field.");
        }
    }
}
