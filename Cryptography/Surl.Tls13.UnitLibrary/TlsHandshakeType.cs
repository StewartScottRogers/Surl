namespace Surl.Tls13;

/// <summary>The handshake message types of RFC 8446 section 4.</summary>
public enum TlsHandshakeType : byte
{
    /// <summary>client_hello (1).</summary>
    ClientHello = 1,

    /// <summary>server_hello (2), which is also a HelloRetryRequest.</summary>
    ServerHello = 2,

    /// <summary>new_session_ticket (4).</summary>
    NewSessionTicket = 4,

    /// <summary>end_of_early_data (5).</summary>
    EndOfEarlyData = 5,

    /// <summary>encrypted_extensions (8).</summary>
    EncryptedExtensions = 8,

    /// <summary>certificate (11).</summary>
    Certificate = 11,

    /// <summary>certificate_request (13).</summary>
    CertificateRequest = 13,

    /// <summary>certificate_verify (15).</summary>
    CertificateVerify = 15,

    /// <summary>finished (20).</summary>
    Finished = 20,

    /// <summary>key_update (24).</summary>
    KeyUpdate = 24,

    /// <summary>message_hash (254): the synthetic message that replaces ClientHello1 in the transcript after a HelloRetryRequest.</summary>
    MessageHash = 254,
}
