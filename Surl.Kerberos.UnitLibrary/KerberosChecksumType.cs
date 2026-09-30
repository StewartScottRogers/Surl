namespace Surl.Kerberos;

/// <summary>
/// The keyed checksum type each accepted encryption type brings, numbered as the IANA Kerberos
/// "Checksum Type Numbers" registry numbers them.
/// </summary>
internal enum KerberosChecksumType
{
    /// <summary><c>hmac-sha1-96-aes128</c> (RFC 3962), the checksum of enctype 17.</summary>
    HmacSha196Aes128 = 15,

    /// <summary><c>hmac-sha1-96-aes256</c> (RFC 3962), the checksum of enctype 18.</summary>
    HmacSha196Aes256 = 16,

    /// <summary><c>hmac-sha256-128-aes128</c> (RFC 8009), the checksum of enctype 19.</summary>
    HmacSha256128Aes128 = 19,

    /// <summary><c>hmac-sha384-192-aes256</c> (RFC 8009), the checksum of enctype 20.</summary>
    HmacSha384192Aes256 = 20,
}
