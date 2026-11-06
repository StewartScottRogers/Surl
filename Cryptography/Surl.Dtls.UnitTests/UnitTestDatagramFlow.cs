using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Dtls;

/// <summary>
/// An in-memory <see cref="IDatagramFlow"/> between <see cref="DtlsServerSession"/> and a
/// <see cref="UnitTestDtlsClient"/>: every datagram the server sends reaches the client at once,
/// unless the test drops it, and the client's answers queue for the server. When the server waits
/// and nothing is queued, the flow lets time pass on the <see cref="ManualTimeProvider"/> a second
/// at a time, so timers fire; after <see cref="SilenceLimit"/> of silence in all it cancels
/// <see cref="Exhausted"/>, which the tests pass to the session as its cancellation token.
/// </summary>
internal sealed class UnitTestDatagramFlow : IDatagramFlow
{
    public static readonly TimeSpan SilenceLimit = TimeSpan.FromMinutes(15);

    private readonly UnitTestDtlsClient client;
    private readonly Queue<byte[]> toServer = new();
    private TimeSpan silence;

    public UnitTestDatagramFlow(UnitTestDtlsClient client, ManualTimeProvider clock, byte[]? firstDatagram = null)
    {
        this.client = client;
        Clock = clock;
        FirstDatagram = firstDatagram ?? client.FirstHelloDatagram();
    }

    public ManualTimeProvider Clock { get; }

    public CancellationTokenSource Exhausted { get; } = new();

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 5684);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public ReadOnlyMemory<byte> FirstDatagram { get; }

    public List<byte[]> Sent { get; } = [];

    /// <summary>Indexes in <see cref="Sent"/> of server datagrams the client never receives.</summary>
    public HashSet<int> DroppedSends { get; } = [];

    public TimeSpan Elapsed { get; private set; }

    public void DeliverFromClient(byte[] datagram) => toServer.Enqueue(datagram);

    public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (toServer.TryDequeue(out var datagram))
            {
                return ValueTask.FromResult<ReadOnlyMemory<byte>>(datagram);
            }

            if (silence >= SilenceLimit)
            {
                Exhausted.Cancel();
                continue;
            }

            silence += TimeSpan.FromSeconds(1);
            Elapsed += TimeSpan.FromSeconds(1);
            Clock.Advance(TimeSpan.FromSeconds(1));
        }
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = datagram.ToArray();
        Sent.Add(bytes);
        if (!DroppedSends.Contains(Sent.Count - 1))
        {
            foreach (var answer in client.OnServerDatagram(bytes))
            {
                toServer.Enqueue(answer);
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Exhausted.Dispose();
        return ValueTask.CompletedTask;
    }
}
