namespace Surl.Protocol.Smb;

/// <summary>
/// The server's answer to one SMB request: the framed response, and whether the connection
/// closes after it (ADR-0073, decision 5), or that a fault rule dropped the connection instead.
/// </summary>
/// <param name="Response">The framed response to write.</param>
/// <param name="ClosesConnection">Whether the server closes the connection once the response is written.</param>
/// <param name="WasDropped">Whether a <c>drop</c> fault rule aborted the connection: nothing is written and nothing more is read.</param>
internal sealed record SmbAnswer(byte[] Response, bool ClosesConnection = false, bool WasDropped = false);
