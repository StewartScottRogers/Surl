namespace Surl.TlsSrp;

/// <summary>
/// The handshake messages a TLS-SRP handshake exchanges (RFC 5246 section 7.4, RFC 5054 section 2.2).
/// </summary>
public enum TlsHandshakeType : byte
{
    /// <summary><c>client_hello</c> (1).</summary>
    ClientHello = 1,

    /// <summary><c>server_hello</c> (2).</summary>
    ServerHello = 2,

    /// <summary><c>server_key_exchange</c> (12): carries <c>N</c>, <c>g</c>, <c>s</c> and <c>B</c>.</summary>
    ServerKeyExchange = 12,

    /// <summary><c>server_hello_done</c> (14).</summary>
    ServerHelloDone = 14,

    /// <summary><c>client_key_exchange</c> (16): carries <c>A</c>.</summary>
    ClientKeyExchange = 16,

    /// <summary><c>finished</c> (20).</summary>
    Finished = 20,
}
