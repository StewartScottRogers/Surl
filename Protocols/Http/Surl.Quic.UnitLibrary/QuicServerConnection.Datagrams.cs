namespace Surl.Quic;

/// <summary>How the connection receives and sends <c>DATAGRAM</c> frames (RFC 9221, ADR-0086 decision 4).</summary>
public sealed partial class QuicServerConnection
{
    private List<QuicDatagramFrame> receivedDatagramFrames = [];

    /// <summary>
    /// Sends <paramref name="data"/> in one <c>DATAGRAM</c> frame (type <c>0x31</c>, RFC 9221
    /// section 4). It is ack-eliciting and counted by congestion control, and never sent again
    /// when lost (section 5).
    /// </summary>
    /// <param name="data">The application data.</param>
    /// <returns>The datagrams to send.</returns>
    /// <exception cref="InvalidOperationException">
    /// The handshake is not complete, the connection has ended, or the client's
    /// <c>max_datagram_frame_size</c> is absent or 0, so it takes no <c>DATAGRAM</c> frames (section 3).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The frame would be larger than the client's <c>max_datagram_frame_size</c> or than
    /// <see cref="MaxApplicationFrameLength"/>.
    /// </exception>
    public QuicConnectionOutput SendDatagram(ReadOnlyMemory<byte> data)
    {
        RequireOneRtt();
        if (PeerTransportParameters!.MaxDatagramFrameSize is not ({ } peerLimit and > 0))
        {
            throw new InvalidOperationException("The client's max_datagram_frame_size is absent or 0: it takes no DATAGRAM frames (RFC 9221 section 3).");
        }

        var frame = new QuicDatagramFrame(data);
        var length = (ulong)frame.ToBytes().Length;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, Math.Min(peerLimit, MaxApplicationFrameLength), nameof(data));
        application.FramesToSend.Add(frame);
        return new QuicConnectionOutput(Flush(), []);
    }

    /// <summary>
    /// Takes a <c>DATAGRAM</c> frame for the caller; one the server's options did not offer, or
    /// one larger than they offered, is <c>PROTOCOL_VIOLATION</c> (RFC 9221 section 3).
    /// </summary>
    private void ReceiveDatagramFrame(QuicDatagramFrame frame)
    {
        var length = (ulong)frame.ToBytes().Length;
        if (options.MaxDatagramFrameSize is not { } limit || length > limit)
        {
            throw new QuicTransportException(
                QuicTransportErrorCode.ProtocolViolation,
                $"A DATAGRAM frame of {length} bytes arrived; the server's max_datagram_frame_size is {options.MaxDatagramFrameSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "not sent"} (RFC 9221 section 3).",
                frame.HasLength ? QuicFrameTypes.DatagramWithLength : QuicFrameTypes.Datagram);
        }

        receivedDatagramFrames.Add(frame);
    }

    /// <summary>The <c>DATAGRAM</c> frames received since the last call.</summary>
    private List<QuicDatagramFrame> TakeReceivedDatagramFrames()
    {
        var taken = receivedDatagramFrames;
        receivedDatagramFrames = [];
        return taken;
    }
}
