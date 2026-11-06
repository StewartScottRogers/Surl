namespace Surl.Tls13;

/// <summary>One extension of an extension block (RFC 8446 section 4.2): its type and its data as they appear on the wire.</summary>
/// <param name="Type">The extension type; a value <see cref="TlsExtensionType"/> does not name is kept as it came.</param>
/// <param name="Data">The extension data, without its two-byte length.</param>
public sealed record TlsExtension(TlsExtensionType Type, ReadOnlyMemory<byte> Data);
