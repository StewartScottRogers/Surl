namespace Surl.Kerberos;

/// <summary>
/// What a key derived from a base key and a key usage is for, valued as the octet that follows
/// the four-octet key usage in the derivation constant (RFC 3961 section 5.3, RFC 8009 section 5).
/// </summary>
internal enum KerberosDerivedKeyPurpose
{
    /// <summary>Kc, the checksum key: constant octet <c>0x99</c>.</summary>
    Checksum = 0x99,

    /// <summary>Ke, the encryption key: constant octet <c>0xAA</c>.</summary>
    Encryption = 0xAA,

    /// <summary>Ki, the integrity key: constant octet <c>0x55</c>.</summary>
    Integrity = 0x55,
}
