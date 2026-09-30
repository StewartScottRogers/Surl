namespace Surl.Protocol.Smb;

/// <summary>
/// An SMB version 1 request, decoded from its parameter and data blocks.
/// </summary>
/// <param name="Header">The request's header, whose TID, PID, UID and MID the response echoes.</param>
internal abstract record SmbRequest(SmbHeader Header);
