namespace Surl.Protocol.Smb;

/// <summary>
/// <c>SMB_COM_TREE_CONNECT_ANDX</c> ([MS-CIFS] section 2.2.4.55.1).
/// </summary>
/// <param name="Header">The request's header.</param>
/// <param name="Flags">The tree connect flags.</param>
/// <param name="Password">The share-level password; empty with user-level security.</param>
/// <param name="Path">The share, as <c>\\host\share</c>.</param>
/// <param name="Service">The service type asked for; curl asks for <c>?????</c>, any.</param>
internal sealed record SmbTreeConnectRequest(SmbHeader Header, ushort Flags, byte[] Password, string Path, string Service) : SmbRequest(Header);
