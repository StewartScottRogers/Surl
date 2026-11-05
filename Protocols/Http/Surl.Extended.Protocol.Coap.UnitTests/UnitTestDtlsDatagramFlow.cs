using System.Net;
using System.Threading.Channels;
using Surl.Dtls;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// An in-memory <see cref="IDatagramFlow"/> between a <c>coaps</c> server and a
/// <see cref="UnitTestDtlsClient"/>: every datagram the server sends reaches the client at once and
/// the client's answers queue for the server; a test adds CoAP messages, sealed as the client's
/// epoch 1 application data, and plaintext datagrams of its own. Time never moves by itself.
/// </summary>
internal sealed class UnitTestDtlsDatagramFlow : IDatagramFlow
{
    private readonly Lock gate = new();
    private readonly Channel<ReadOnlyMemory<byte>> toServer = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private readonly List<byte[]> sent = [];

    public UnitTestDtlsDatagramFlow(UnitTestDtlsClient client, byte[]? firstDatagram = null)
    {
        Client = client;
        FirstDatagram = firstDatagram ?? client.FirstHelloDatagram();
        if (firstDatagram is not null)
        {
            toServer.Writer.TryWrite(client.FirstHelloDatagram());
        }
    }

    public UnitTestDtlsClient Client { get; }

    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 5684);

    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    public ReadOnlyMemory<byte> FirstDatagram { get; }

    /// <summary>A copy of every datagram the server sent, in order.</summary>
    public IReadOnlyList<byte[]> Sent
    {
        get
        {
            lock (gate)
            {
                return [.. sent];
            }
        }
    }

    /// <summary>The CoAP messages the client has decrypted, in order.</summary>
    public IReadOnlyList<byte[]> ReceivedCoap
    {
        get
        {
            lock (gate)
            {
                return [.. Client.ReceivedApplicationData];
            }
        }
    }

    /// <summary>The alerts the client has received, in order.</summary>
    public IReadOnlyList<DtlsAlert> ReceivedAlerts
    {
        get
        {
            lock (gate)
            {
                return [.. Client.ReceivedAlerts];
            }
        }
    }

    public bool IsEstablished
    {
        get
        {
            lock (gate)
            {
                return Client.IsEstablished;
            }
        }
    }

    /// <summary>Sends <paramref name="coap"/> as one epoch 1 application data record.</summary>
    public void SendCoap(byte[] coap) => SendProtected(DtlsContentType.ApplicationData, coap);

    /// <summary>Sends one epoch 1 record of <paramref name="contentType"/>.</summary>
    public void SendProtected(DtlsContentType contentType, byte[] plaintext)
    {
        byte[] record;
        lock (gate)
        {
            record = Client.ProtectedRecord(contentType, plaintext);
        }

        toServer.Writer.TryWrite(record);
    }

    /// <summary>Sends a datagram exactly as given, such as plaintext CoAP.</summary>
    public void SendRaw(byte[] datagram) => toServer.Writer.TryWrite(datagram);

    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) =>
        await toServer.Reader.ReadAsync(cancellationToken);

    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = datagram.ToArray();
        List<byte[]> answers;
        lock (gate)
        {
            sent.Add(bytes);
            answers = Client.OnServerDatagram(bytes);
        }

        foreach (var answer in answers)
        {
            toServer.Writer.TryWrite(answer);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
