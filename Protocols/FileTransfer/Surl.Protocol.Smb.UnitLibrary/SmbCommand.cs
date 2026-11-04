namespace Surl.Protocol.Smb;

/// <summary>
/// The SMB version 1 command codes upstream curl 8.21.0 sends (<c>lib/smb.c</c>), as [MS-CIFS]
/// section 2.2.2.1 numbers them.
/// </summary>
internal static class SmbCommand
{
    /// <summary><c>SMB_COM_CLOSE</c>.</summary>
    public const byte Close = 0x04;

    /// <summary><c>SMB_COM_READ_ANDX</c>.</summary>
    public const byte ReadAndX = 0x2E;

    /// <summary><c>SMB_COM_WRITE_ANDX</c>.</summary>
    public const byte WriteAndX = 0x2F;

    /// <summary><c>SMB_COM_TREE_DISCONNECT</c>.</summary>
    public const byte TreeDisconnect = 0x71;

    /// <summary><c>SMB_COM_NEGOTIATE</c>.</summary>
    public const byte Negotiate = 0x72;

    /// <summary><c>SMB_COM_SESSION_SETUP_ANDX</c>.</summary>
    public const byte SessionSetupAndX = 0x73;

    /// <summary><c>SMB_COM_TREE_CONNECT_ANDX</c>.</summary>
    public const byte TreeConnectAndX = 0x75;

    /// <summary><c>SMB_COM_NT_CREATE_ANDX</c>.</summary>
    public const byte NtCreateAndX = 0xA2;

    /// <summary><c>SMB_COM_NO_ANDX_COMMAND</c>: the AndX command that ends a chain, the only one curl sends.</summary>
    public const byte NoAndXCommand = 0xFF;
}
