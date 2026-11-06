namespace Surl.Protocol.Smb;

/// <summary>
/// <c>SMB_COM_NEGOTIATE</c> ([MS-CIFS] section 2.2.4.52.1): the dialects the client offers, in order.
/// </summary>
/// <param name="Header">The request's header.</param>
/// <param name="Dialects">The dialect strings; the response names one by its index.</param>
internal sealed record SmbNegotiateRequest(SmbHeader Header, IReadOnlyList<string> Dialects) : SmbRequest(Header);
