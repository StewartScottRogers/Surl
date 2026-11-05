namespace Surl.Dtls;

/// <summary>
/// The <c>msg_type</c> of a DTLS 1.2 handshake message (RFC 5246 section 7.4, with RFC 6347
/// section 4.3.2's <c>hello_verify_request</c>).
/// </summary>
public enum DtlsHandshakeType : byte
{
    /// <summary><c>hello_request</c> (0); the server never sends it (ADR-0087 decision 6).</summary>
    HelloRequest = 0,

    /// <summary><c>client_hello</c> (1).</summary>
    ClientHello = 1,

    /// <summary><c>server_hello</c> (2).</summary>
    ServerHello = 2,

    /// <summary><c>hello_verify_request</c> (3), RFC 6347 section 4.2.1.</summary>
    HelloVerifyRequest = 3,

    /// <summary><c>certificate</c> (11).</summary>
    Certificate = 11,

    /// <summary><c>server_key_exchange</c> (12).</summary>
    ServerKeyExchange = 12,

    /// <summary><c>certificate_request</c> (13).</summary>
    CertificateRequest = 13,

    /// <summary><c>server_hello_done</c> (14).</summary>
    ServerHelloDone = 14,

    /// <summary><c>certificate_verify</c> (15).</summary>
    CertificateVerify = 15,

    /// <summary><c>client_key_exchange</c> (16).</summary>
    ClientKeyExchange = 16,

    /// <summary><c>finished</c> (20).</summary>
    Finished = 20,
}
