namespace Surl.TlsSrp;

/// <summary>One handshake message as read from the client (RFC 5246 section 7.4).</summary>
/// <param name="Type">The message type byte.</param>
/// <param name="Body">The body, without the four-byte header.</param>
/// <param name="Framed">The header and the body, as they enter the handshake hash.</param>
internal sealed record TlsHandshakeMessage(byte Type, byte[] Body, byte[] Framed);
