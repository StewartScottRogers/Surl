namespace Surl.Quic;

/// <summary>
/// One QUIC packet's header, with what follows it, as <see cref="QuicPacketReader"/> reads it
/// from a datagram (RFC 9000 section 17). The concrete types are
/// <see cref="QuicLongHeaderPacket"/>, <see cref="QuicRetryPacket"/>,
/// <see cref="QuicVersionNegotiationPacket"/>, <see cref="QuicShortHeaderPacket"/> and
/// <see cref="QuicUnsupportedVersionPacket"/>.
/// </summary>
public abstract record QuicPacket
{
    private protected QuicPacket()
    {
    }

    /// <summary>The Destination Connection ID; when read, a slice of the datagram.</summary>
    public abstract ReadOnlyMemory<byte> DestinationConnectionId { get; }
}
