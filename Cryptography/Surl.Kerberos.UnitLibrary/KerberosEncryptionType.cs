namespace Surl.Kerberos;

/// <summary>
/// The Kerberos encryption types surl accepts (ADR-0057 decision 3), numbered as the IANA
/// Kerberos "Encryption Type Numbers" registry numbers them.
/// </summary>
public enum KerberosEncryptionType
{
    /// <summary><c>aes128-cts-hmac-sha1-96</c> (RFC 3962).</summary>
    Aes128CtsHmacSha196 = 17,

    /// <summary><c>aes256-cts-hmac-sha1-96</c> (RFC 3962).</summary>
    Aes256CtsHmacSha196 = 18,

    /// <summary><c>aes128-cts-hmac-sha256-128</c> (RFC 8009).</summary>
    Aes128CtsHmacSha256128 = 19,

    /// <summary><c>aes256-cts-hmac-sha384-192</c> (RFC 8009).</summary>
    Aes256CtsHmacSha384192 = 20,
}
