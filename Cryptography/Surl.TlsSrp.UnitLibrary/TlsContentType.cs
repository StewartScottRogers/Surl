namespace Surl.TlsSrp;

/// <summary>
/// The content type a TLS 1.2 record carries (RFC 5246 section 6.2.1).
/// </summary>
public enum TlsContentType : byte
{
    /// <summary><c>change_cipher_spec</c> (20).</summary>
    ChangeCipherSpec = 20,

    /// <summary><c>alert</c> (21).</summary>
    Alert = 21,

    /// <summary><c>handshake</c> (22).</summary>
    Handshake = 22,

    /// <summary><c>application_data</c> (23).</summary>
    ApplicationData = 23,
}
