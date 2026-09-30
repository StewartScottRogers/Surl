namespace Surl.Protocol.Ssh;

/// <summary>
/// One SFTP request packet after its length: its type, its id, and its fields read front to back
/// (draft-ietf-secsh-filexfer-02, section 3). A field that runs past the packet, or bytes left
/// over once every field is read, make it a packet that does not parse, answered
/// <c>BAD_MESSAGE</c> (ADR-0054, decision 5).
/// </summary>
internal sealed class SftpRequest
{
    private const uint AttributeSize = 0x00000001;
    private const uint AttributeOwners = 0x00000002;
    private const uint AttributePermissions = 0x00000004;
    private const uint AttributeTimes = 0x00000008;
    private const uint AttributeExtended = 0x80000000;

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
    /// Reads an <c>ATTRS</c> field (draft-02, section 5) and discards it: the read side keeps no
    /// attribute a client sends.
    /// </summary>
    /// <exception cref="SshDisconnectRequiredException">A field runs past the packet.</exception>
    public void SkipAttributes()
    {
        var flags = fields.ReadUInt32();
        fields.ReadBytes(FixedAttributeBytes(flags));
        if ((flags & AttributeExtended) != 0)
        {
            SkipExtendedAttributes();
        }
    }

    // The size, the owners, the permissions and the times, each present when its flag is set.
    private static int FixedAttributeBytes(uint flags) =>
        ((flags & AttributeSize) != 0 ? 8 : 0)
        + ((flags & AttributeOwners) != 0 ? 8 : 0)
        + ((flags & AttributePermissions) != 0 ? 4 : 0)
        + ((flags & AttributeTimes) != 0 ? 8 : 0);

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
