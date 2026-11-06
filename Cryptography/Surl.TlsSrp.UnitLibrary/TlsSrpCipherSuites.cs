namespace Surl.TlsSrp;

/// <summary>
/// What each of RFC 5054's suites encrypts with. Every one MACs with HMAC-SHA1 (20-byte keys and
/// tags) and derives its keys with the SHA-256 PRF (ADR-0122 decision 2).
/// </summary>
public static class TlsSrpCipherSuites
{
    /// <summary>The length of an HMAC-SHA1 key and tag: 20 bytes.</summary>
    public const int MacLength = 20;

    /// <summary>The block cipher <paramref name="suite"/> encrypts records with.</summary>
    /// <param name="suite">One of RFC 5054 section 2.7's suites.</param>
    /// <returns>Its bulk cipher.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="suite"/> is not one of the nine.</exception>
    public static TlsBulkCipher BulkCipherOf(TlsSrpCipherSuite suite) => suite switch
    {
        TlsSrpCipherSuite.SrpSha3DesEdeCbcSha or TlsSrpCipherSuite.SrpShaRsa3DesEdeCbcSha
            or TlsSrpCipherSuite.SrpShaDss3DesEdeCbcSha => TlsBulkCipher.TripleDesEdeCbc,
        TlsSrpCipherSuite.SrpShaAes128CbcSha or TlsSrpCipherSuite.SrpShaRsaAes128CbcSha
            or TlsSrpCipherSuite.SrpShaDssAes128CbcSha => TlsBulkCipher.Aes128Cbc,
        TlsSrpCipherSuite.SrpShaAes256CbcSha or TlsSrpCipherSuite.SrpShaRsaAes256CbcSha
            or TlsSrpCipherSuite.SrpShaDssAes256CbcSha => TlsBulkCipher.Aes256Cbc,
        _ => throw new ArgumentOutOfRangeException(nameof(suite), suite, "Not an RFC 5054 cipher suite."),
    };

    /// <summary>The encryption key length of <paramref name="cipher"/>, in bytes.</summary>
    /// <param name="cipher">The bulk cipher.</param>
    /// <returns>24 for 3DES, 16 for AES-128, 32 for AES-256.</returns>
    public static int KeyLengthOf(TlsBulkCipher cipher) => cipher switch
    {
        TlsBulkCipher.TripleDesEdeCbc => 24,
        TlsBulkCipher.Aes128Cbc => 16,
        _ => 32,
    };

    /// <summary>The block length of <paramref name="cipher"/>, which is also its explicit IV's length.</summary>
    /// <param name="cipher">The bulk cipher.</param>
    /// <returns>8 for 3DES, 16 for AES.</returns>
    public static int BlockLengthOf(TlsBulkCipher cipher) =>
        cipher == TlsBulkCipher.TripleDesEdeCbc ? 8 : 16;
}
