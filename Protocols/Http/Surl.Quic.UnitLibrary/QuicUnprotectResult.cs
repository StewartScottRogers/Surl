namespace Surl.Quic;

/// <summary>The outcome of <see cref="QuicPacketProtection.Unprotect(ReadOnlyMemory{byte}, int, ulong)"/> for one packet.</summary>
/// <param name="Status">Whether the packet opened.</param>
/// <param name="Packet">
/// When <paramref name="Status"/> is <see cref="QuicUnprotectStatus.Unprotected"/>, the packet in
/// the clear - a <see cref="QuicLongHeaderPacket"/> or <see cref="QuicShortHeaderPacket"/> whose
/// <c>Payload</c> is the decrypted frames, without the AEAD tag; otherwise <see langword="null"/>.
/// </param>
/// <param name="PacketNumber">The full packet number; 0 when the packet was too short to sample.</param>
/// <param name="PacketLength">How many bytes of the datagram the packet took, so the next coalesced packet starts after them.</param>
public sealed record QuicUnprotectResult(QuicUnprotectStatus Status, QuicPacket? Packet, ulong PacketNumber, int PacketLength);
