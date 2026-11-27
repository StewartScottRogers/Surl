using Surl.Extended.Protocol.DnsQuery;
using Surl.Http3;
using Surl.Protocol.Abstractions;
using Surl.Quic;

namespace Surl.Composition;

/// <summary>
/// A hand-built DNS over QUIC client for composition tests, on the far side of an <see cref="InMemoryDatagramFlow"/>:
/// its QUIC connection opens with the Initials pinned upstream curl sent, the ALPN list edited to <c>doq</c>
/// (<see cref="DnsOverQuicClientHello"/>), then it sends one query framed as RFC 9250 section 4.2 says.
/// </summary>
internal sealed class CompositionDnsOverQuicClient : IDisposable
{
    private int taken;

    public CompositionDnsOverQuicClient()
    {
        Quic = UnitTestQuicClient.FromRecording(CompositionHttp3QuicClient.ReadRecordedInitials(), DnsOverQuicClientHello.OfferDoq);
        var datagrams = Quic.InitialDatagrams.Select(datagram => (ReadOnlyMemory<byte>)datagram).ToList();
        Flow = new InMemoryDatagramFlow(datagrams[0], datagrams.Skip(1));
    }

    /// <summary>The flow the server accepts.</summary>
    public InMemoryDatagramFlow Flow { get; }

    private UnitTestQuicClient Quic { get; }

    /// <summary>The <c>CONNECTION_CLOSE</c> frames received in 1-RTT packets, each repeat included.</summary>
    public IEnumerable<QuicConnectionCloseFrame> Closes => OneRttFrames<QuicConnectionCloseFrame>();

    /// <summary>How many bytes of stream data have arrived on stream 0.</summary>
    public int AnsweredByteCount => OneRttFrames<QuicStreamFrame>().Where(frame => frame.StreamId == 0).Sum(frame => frame.Data.Length);

    /// <summary>Completes the handshake, then sends <paramref name="message"/> after its two-byte length on stream 0, with <c>FIN</c>.</summary>
    public async Task QueryAsync(byte[] message, CancellationToken cancellationToken)
    {
        await UntilAsync(() => Quic.ClientFinished is not null, cancellationToken);
        Flow.DeliverFromClient(Quic.FinishedDatagram());
        byte[] framed = [(byte)(message.Length >> 8), (byte)message.Length, .. message];
        Flow.DeliverFromClient(Quic.SealOneRtt(new QuicStreamFrame(0, 0, framed, true)));
    }

    /// <summary>
    /// Opens what the server has sent until <paramref name="condition"/> holds; gives up after 30 seconds, or when
    /// the token is cancelled.
    /// </summary>
    public async Task UntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            var sent = Flow.SentDatagrams;
            Quic.Receive(sent.Skip(taken));
            taken = sent.Count;
            if (condition())
            {
                return;
            }

            await Task.Delay(1, patience.Token);
        }
    }

    public void Dispose() => Quic.Dispose();

    private IEnumerable<T> OneRttFrames<T>()
        where T : QuicFrame => Quic.FramesReceived[QuicPacketType.OneRtt].OfType<T>();
}
