namespace Surl.Tls13;

/// <summary>The TLS record content types of RFC 8446 section 5.1 that a TLS 1.3 record can carry.</summary>
public enum TlsContentType : byte
{
    /// <summary>change_cipher_spec (20): only the compatibility record of RFC 8446 appendix D.4, which carries no protection.</summary>
    ChangeCipherSpec = 20,

    /// <summary>alert (21).</summary>
    Alert = 21,

    /// <summary>handshake (22).</summary>
    Handshake = 22,

    /// <summary>application_data (23): the outer type of every protected record, and the inner type of application bytes.</summary>
    ApplicationData = 23,
}
