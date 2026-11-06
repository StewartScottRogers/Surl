namespace Surl.TlsSrp;

/// <summary>
/// The fixed numbers of a TLS 1.2 record (RFC 5246 section 6.2): its five-byte header, the version
/// the server writes, and the length limits the reader enforces.
/// </summary>
public static class TlsRecordFormat
{
    /// <summary>The header: content type (1), version (2), length (2).</summary>
    public const int HeaderLength = 5;

    /// <summary>The version the server writes in every record: TLS 1.2, <c>03 03</c>.</summary>
    public const ushort Tls12Version = 0x0303;

    /// <summary>
    /// The longest <c>TLSPlaintext.fragment</c>, before protection or after it is removed: 2^14
    /// bytes (RFC 5246 section 6.2.1).
    /// </summary>
    public const int MaxPlaintextLength = 1 << 14;

    /// <summary>
    /// The longest <c>TLSCiphertext.fragment</c>: 2^14 + 2048 bytes (RFC 5246 section 6.2.3).
    /// </summary>
    public const int MaxCiphertextLength = MaxPlaintextLength + 2048;
}
