namespace Surl.Quic;

/// <summary>The kinds of QUIC version 1 packet (RFC 9000 section 17).</summary>
public enum QuicPacketType
{
    /// <summary>An Initial packet: long header type <c>0x0</c>, carrying the first <c>CRYPTO</c> data.</summary>
    Initial,

    /// <summary>A 0-RTT packet: long header type <c>0x1</c>, carrying early application data.</summary>
    ZeroRtt,

    /// <summary>A Handshake packet: long header type <c>0x2</c>, carrying the rest of the TLS handshake.</summary>
    Handshake,

    /// <summary>A Retry packet: long header type <c>0x3</c>, carrying a token for address validation.</summary>
    Retry,

    /// <summary>A Version Negotiation packet: a long header with version 0, listing the versions a server supports.</summary>
    VersionNegotiation,

    /// <summary>A 1-RTT packet: the short header, carrying application data once the handshake has keys.</summary>
    OneRtt,
}
