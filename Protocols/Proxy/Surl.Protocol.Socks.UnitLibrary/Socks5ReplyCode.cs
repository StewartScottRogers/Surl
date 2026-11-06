namespace Surl.Protocol.Socks;

/// <summary>
/// The SOCKS5 reply's <c>REP</c> field (RFC 1928 section 6), each sent in the case ADR-0121
/// decision 3's table names.
/// </summary>
internal enum Socks5ReplyCode : byte
{
    /// <summary><c>00</c>: the origin accepted.</summary>
    Succeeded = 0x00,

    /// <summary><c>01</c>: any failure the codes below do not name.</summary>
    GeneralFailure = 0x01,

    /// <summary><c>02</c>: <see cref="Abstractions.OriginConnectionFailure.NotAllowed"/>, the address <c>0.0.0.0</c> or <c>::</c>, or port 0.</summary>
    NotAllowed = 0x02,

    /// <summary><c>03</c>: <see cref="Abstractions.OriginConnectionFailure.Unreachable"/>.</summary>
    NetworkUnreachable = 0x03,

    /// <summary><c>04</c>: <see cref="Abstractions.OriginConnectionFailure.HostNotFound"/>, the empty name included.</summary>
    HostUnreachable = 0x04,

    /// <summary><c>05</c>: <see cref="Abstractions.OriginConnectionFailure.Refused"/>.</summary>
    ConnectionRefused = 0x05,

    /// <summary><c>06</c>: <see cref="Abstractions.OriginConnectionFailure.TimedOut"/>.</summary>
    TtlExpired = 0x06,

    /// <summary><c>07</c>: a command other than <c>CONNECT</c>, <c>BIND</c> and <c>UDP ASSOCIATE</c> included.</summary>
    CommandNotSupported = 0x07,

    /// <summary><c>08</c>: an <c>ATYP</c> other than <c>01</c>, <c>03</c> or <c>04</c>.</summary>
    AddressTypeNotSupported = 0x08,
}
