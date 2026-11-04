namespace Surl.Protocol.Smb;

/// <summary>
/// <c>SMB_COM_TREE_DISCONNECT</c> ([MS-CIFS] section 2.2.4.51.1): no parameters, no data.
/// </summary>
/// <param name="Header">The request's header, whose TID names the tree to disconnect.</param>
internal sealed record SmbTreeDisconnectRequest(SmbHeader Header) : SmbRequest(Header);
