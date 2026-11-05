namespace Surl.Quic;

/// <summary>
/// A packet the server sent and has not yet seen acknowledged or declared lost (RFC 9002
/// Appendix A.1.1), with the frames it carried so the lost ones can be sent again.
/// </summary>
/// <param name="PacketNumber">Its packet number.</param>
/// <param name="TimeSent">When it was sent.</param>
/// <param name="IsAckEliciting">Whether it carried a frame other than ACK, PADDING and CONNECTION_CLOSE.</param>
/// <param name="IsInFlight">Whether it counts in flight: ack-eliciting or padded.</param>
/// <param name="SentBytes">Its size on the wire.</param>
/// <param name="Frames">The frames it carried.</param>
internal sealed record QuicSentPacket(ulong PacketNumber, DateTimeOffset TimeSent, bool IsAckEliciting, bool IsInFlight, int SentBytes, IReadOnlyList<QuicFrame> Frames);
