namespace Surl.TlsSrp;

/// <summary>
/// One TLS 1.2 record as the reader returns it: its header's content type and version, and its
/// fragment with any protection removed (RFC 5246 section 6.2).
/// </summary>
/// <param name="ContentType">The record's content type.</param>
/// <param name="Version">The record header's version, as sent (a ClientHello's record may say <c>03 01</c>).</param>
/// <param name="Fragment">The plaintext fragment: at most 2^14 bytes.</param>
public sealed record TlsRecord(TlsContentType ContentType, ushort Version, byte[] Fragment);
