namespace Surl.Protocol.Smb;

/// <summary>
/// <c>SMB_COM_SESSION_SETUP_ANDX</c> in its NT LM 0.12 form ([MS-CIFS] section 2.2.4.53.1), with
/// ASCII strings, as curl sends it with no <c>SMB_FLAGS2_UNICODE</c>.
/// </summary>
/// <param name="Header">The request's header.</param>
/// <param name="MaxBufferSize">The largest message the client accepts.</param>
/// <param name="MaxMpxCount">How many requests the client may have outstanding.</param>
/// <param name="VirtualCircuitNumber">The virtual circuit number.</param>
/// <param name="SessionKey">The session key the negotiate response gave.</param>
/// <param name="Capabilities">The client's capabilities.</param>
/// <param name="LmResponse">The <c>OEMPassword</c> field: the LM challenge response.</param>
/// <param name="NtResponse">The <c>UnicodePassword</c> field: the NT challenge response.</param>
/// <param name="AccountName">The user name.</param>
/// <param name="PrimaryDomain">The user's domain.</param>
/// <param name="NativeOperatingSystem">The client's operating system.</param>
/// <param name="NativeLanManager">The client's LAN manager.</param>
internal sealed record SmbSessionSetupRequest(
    SmbHeader Header,
    ushort MaxBufferSize,
    ushort MaxMpxCount,
    ushort VirtualCircuitNumber,
    uint SessionKey,
    uint Capabilities,
    byte[] LmResponse,
    byte[] NtResponse,
    string AccountName,
    string PrimaryDomain,
    string NativeOperatingSystem,
    string NativeLanManager) : SmbRequest(Header);
