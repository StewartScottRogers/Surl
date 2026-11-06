namespace Surl.TlsSrp;

/// <summary>
/// The CBC block ciphers RFC 5054's suites use (RFC 5246 section 6.2.3.2, ADR-0122 decision 2).
/// </summary>
public enum TlsBulkCipher
{
    /// <summary>3DES-EDE in CBC mode: a 24-byte key and 8-byte blocks.</summary>
    TripleDesEdeCbc,

    /// <summary>AES-128 in CBC mode: a 16-byte key and 16-byte blocks.</summary>
    Aes128Cbc,

    /// <summary>AES-256 in CBC mode: a 32-byte key and 16-byte blocks.</summary>
    Aes256Cbc,
}
