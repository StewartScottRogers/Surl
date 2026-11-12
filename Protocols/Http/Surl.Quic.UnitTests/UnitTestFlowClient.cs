using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Quic;

/// <summary>
/// The test client on the far side of an <see cref="InMemoryDatagramFlow"/>: the flow opens with the
/// client's recorded Initials, and the client opens every datagram a <see cref="QuicDatagramFlowServer"/>
/// sends as the test waits for it. The clock is a <see cref="UnitTestTimeProvider"/> a test moves.
/// </summary>
internal sealed class UnitTestFlowClient : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private int taken;

    /// <param name="editClientHello">An edit to the ClientHello, as <see cref="UnitTestQuicClient.FromRecording"/> takes.</param>
    /// <param name="strayFirstDatagram">A datagram that opens the flow ahead of every recorded Initial; none when null.</param>
    public UnitTestFlowClient(Func<byte[], byte[]>? editClientHello = null, byte[]? strayFirstDatagram = null)
    {
        Client = UnitTestQuicClient.FromRecording(UnitTestExchange.DefaultFixture, editClientHello);
        var datagrams = (strayFirstDatagram is null ? Client.InitialDatagrams : [strayFirstDatagram, .. Client.InitialDatagrams]).Select(datagram => (ReadOnlyMemory<byte>)datagram).ToList();
        Flow = new InMemoryDatagramFlow(datagrams[0], datagrams.Skip(1));
    }

    public UnitTestQuicClient Client { get; }

    public InMemoryDatagramFlow Flow { get; }

    public UnitTestTimeProvider Time { get; } = new();

    /// <summary>The connection the server handed its application, once it has.</summary>
    public QuicApplicationConnection? ApplicationConnection { get; private set; }

    public QuicDatagramFlowServer Server(QuicServerConnectionOptions options, IQuicApplicationProtocol applicationProtocol) =>
        new(options, new RecordingApplicationProtocol(this, applicationProtocol), Time, RandomNumberGenerator.Create());

    /// <summary>The frames of every 1-RTT packet the client has opened.</summary>
    public IEnumerable<T> OneRttFrames<T>() where T : QuicFrame => Client.FramesReceived[QuicPacketType.OneRtt].OfType<T>();

    /// <summary>The bytes the server sent on <paramref name="streamId"/>, by offset, and whether its FIN came.</summary>
    public (byte[] Data, bool Fin) StreamSent(ulong streamId)
    {
        var frames = OneRttFrames<QuicStreamFrame>().Where(frame => frame.StreamId == streamId).ToList();
        var data = new byte[frames.Select(frame => (int)frame.Offset + frame.Data.Length).DefaultIfEmpty(0).Max()];
        foreach (var frame in frames)
        {
            frame.Data.CopyTo(data.AsMemory((int)frame.Offset));
        }

        return (data, frames.Any(frame => frame.Fin));
    }

    /// <summary>Opens what the server has sent until <paramref name="condition"/> holds.</summary>
    public async Task UntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(Patience);
        while (true)
        {
            TakeSent();
            if (condition())
            {
                return;
            }

            await Task.Delay(1, patience.Token);
        }
    }

    /// <summary>Waits for the server's handshake flight and sends the client's Finished.</summary>
    public async Task CompleteHandshakeAsync(CancellationToken cancellationToken)
    {
        await UntilAsync(() => Client.ClientFinished is not null, cancellationToken);
        Flow.DeliverFromClient(Client.FinishedDatagram());
    }

    /// <summary>Sends 1-RTT frames from the client.</summary>
    public void SendOneRtt(params QuicFrame[] frames) => Flow.DeliverFromClient(Client.SealOneRtt(frames));

    /// <summary>
    /// Waits for <paramref name="serving"/> to end, then opens everything the server sent. Once the
    /// connection has closed - the client has opened the server's <c>CONNECTION_CLOSE</c>, or the
    /// application was told the connection ended - it moves the clock past the closing or draining
    /// state, which holds the flow open until then (RFC 9000 section 10.2).
    /// </summary>
    public async Task FinishAsync(Task serving, CancellationToken cancellationToken)
    {
        await UntilAsync(() => serving.IsCompleted || HasClosed(), cancellationToken);
        await UntilAsync(
            () =>
            {
                Time.Advance(ClosingStateBound);
                return serving.IsCompleted;
            },
            cancellationToken);
        await serving;
        TakeSent();
    }

    /// <summary>Whether the connection has closed: the server's <c>CONNECTION_CLOSE</c> reached the client, or the application's side has ended.</summary>
    public bool HasClosed() =>
        ApplicationConnection?.Ended.IsCancellationRequested == true
        || Client.FramesReceived.Values.Any(frames => frames.OfType<QuicConnectionCloseFrame>().Any());

    public void Dispose() => Client.Dispose();

    /// <summary>Longer than any closing or draining state the tests reach: three probe timeouts from the initial 333 ms RTT are about 3 s.</summary>
    private static readonly TimeSpan ClosingStateBound = TimeSpan.FromMinutes(1);

    /// <summary>Records the connection the server hands its application, then runs the test's application.</summary>
    private sealed class RecordingApplicationProtocol(UnitTestFlowClient client, IQuicApplicationProtocol inner) : IQuicApplicationProtocol
    {
        public Task ServeAsync(QuicApplicationConnection connection, CancellationToken cancellationToken)
        {
            client.ApplicationConnection = connection;
            return inner.ServeAsync(connection, cancellationToken);
        }
    }

    private void TakeSent()
    {
        var sent = Flow.SentDatagrams;
        Client.Receive(sent.Skip(taken));
        taken = sent.Count;
    }
}
