namespace Surl.Quic;

/// <summary>What became of a packet <see cref="QuicPacketProtection.Unprotect"/> was given.</summary>
public enum QuicUnprotectStatus
{
    /// <summary>The packet opened: its header and payload are in the clear.</summary>
    Unprotected,

    /// <summary>
    /// The packet did not open - its AEAD tag failed, or it was too short to sample - and is to be
    /// dropped without closing the connection (RFC 9001 sections 5.4.2 and 5.5).
    /// </summary>
    Undecryptable,

    /// <summary>
    /// The packet opened but its reserved bits are not 0: a connection error of type
    /// <c>PROTOCOL_VIOLATION</c> (RFC 9000 sections 17.2 and 17.3.1).
    /// </summary>
    ReservedBitsNotZero,
}
