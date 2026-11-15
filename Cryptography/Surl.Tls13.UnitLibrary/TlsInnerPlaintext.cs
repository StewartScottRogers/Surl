namespace Surl.Tls13;

/// <summary>What a protected record held once unprotected (RFC 8446 section 5.2): the real content type and the content, with padding removed.</summary>
/// <param name="ContentType">The real content type the record carried.</param>
/// <param name="Content">The record's content.</param>
public sealed record TlsInnerPlaintext(TlsContentType ContentType, byte[] Content);
