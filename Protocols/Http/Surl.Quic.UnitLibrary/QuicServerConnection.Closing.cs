namespace Surl.Quic;

/// <summary>
/// The closing and draining states (RFC 9000 section 10.2): after the server sends
/// <c>CONNECTION_CLOSE</c> it answers the client's datagrams with the same datagrams again, at a
/// limited rate, for three probe timeouts; after the client's <c>CONNECTION_CLOSE</c> it sends
/// nothing for the same time. Then the connection state is discarded.
/// </summary>
public sealed partial class QuicServerConnection
{
    /// <summary>How many probe timeouts the closing and draining states last (RFC 9000 section 10.2).</summary>
    public const int ClosingPeriodProbeTimeouts = 3;

    private List<byte[]> connectionCloseDatagrams = [];
    private DateTimeOffset? closingPeriodEnd;
    private long datagramsReceivedWhileClosing;
    private long nextDatagramAnsweredWhileClosing = 1;

    /// <summary>
    /// Whether the server sent <c>CONNECTION_CLOSE</c> and is in the closing state: each datagram
    /// the client sends is answered with the <c>CONNECTION_CLOSE</c> datagrams again, the 1st, 2nd,
    /// 4th, 8th and so on, until <see cref="NextDeadline"/>.
    /// </summary>
    public bool IsClosing { get; private set; }

    /// <summary>Whether the client sent <c>CONNECTION_CLOSE</c> and the server is in the draining state: it sends nothing until <see cref="NextDeadline"/>.</summary>
    public bool IsDraining { get; private set; }

    /// <summary>
    /// How long the closing and draining states last: three probe timeouts as the connection
    /// measures them now, <c>smoothed_rtt + max(4 * rttvar, kGranularity) + max_ack_delay</c>
    /// (RFC 9002 section 6.2.1), without backoff.
    /// </summary>
    private TimeSpan ClosingPeriod => (recovery.Rtt.ProbeTimeout + recovery.MaxAckDelay) * ClosingPeriodProbeTimeouts;

    /// <summary>Enters the closing state once <c>CONNECTION_CLOSE</c> is queued.</summary>
    private void EnterClosingState()
    {
        IsClosing = true;
        closingPeriodEnd = timeProvider.GetUtcNow() + ClosingPeriod;
    }

    /// <summary>Enters the draining state on the client's <c>CONNECTION_CLOSE</c>: nothing more is sent.</summary>
    private void EnterDrainingState()
    {
        Discard();
        IsDraining = true;
        closingPeriodEnd = timeProvider.GetUtcNow() + ClosingPeriod;
    }

    /// <summary>Keeps the datagrams sent in the closing state: they carry the <c>CONNECTION_CLOSE</c> sent again.</summary>
    private void KeepConnectionCloseDatagrams(List<byte[]> datagrams)
    {
        if (IsClosing)
        {
            connectionCloseDatagrams.AddRange(datagrams);
        }
    }

    /// <summary>
    /// Answers a datagram that arrived in the closing state, without reading it: the 1st, 2nd,
    /// 4th, 8th and so on are answered with the <c>CONNECTION_CLOSE</c> datagrams, while the
    /// anti-amplification limit allows them (RFC 9000 sections 8.1 and 10.2.1).
    /// </summary>
    private QuicConnectionOutput AnswerInClosingState(int length)
    {
        bytesReceived += length;
        datagramsReceivedWhileClosing++;
        if (datagramsReceivedWhileClosing < nextDatagramAnsweredWhileClosing)
        {
            return QuicConnectionOutput.Empty;
        }

        nextDatagramAnsweredWhileClosing *= 2;
        return new QuicConnectionOutput(SendConnectionCloseAgain(), []);
    }

    /// <summary>
    /// The <c>CONNECTION_CLOSE</c> datagrams to send again. While the anti-amplification limit
    /// still holds back some of the first ones, those it now allows are built instead, and kept.
    /// </summary>
    private List<byte[]> SendConnectionCloseAgain()
    {
        if (initial.HasSomethingToSend || handshake.HasSomethingToSend || application.HasSomethingToSend)
        {
            return Flush();
        }

        long length = connectionCloseDatagrams.Sum(datagram => datagram.Length);
        if (!addressValidated && bytesSent + length > AmplificationFactor * bytesReceived)
        {
            return [];
        }

        bytesSent += length;
        return connectionCloseDatagrams;
    }

    /// <summary>Ends the closing or draining state: the connection state is discarded and nothing more is answered.</summary>
    private void EndClosingPeriod()
    {
        IsClosing = false;
        IsDraining = false;
        closingPeriodEnd = null;
        connectionCloseDatagrams = [];
    }
}
