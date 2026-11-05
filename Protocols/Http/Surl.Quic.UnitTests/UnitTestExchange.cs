using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>A server connection and the test client talking to it, counting the bytes each way.</summary>
internal sealed class UnitTestExchange : IDisposable
{
    public const string DefaultFixture = "static-8.21.0-win.datagrams.bin";

    public UnitTestExchange(QuicServerConnectionOptions options, string fixture = DefaultFixture, Func<byte[], byte[]>? editClientHello = null)
    {
        Server = new QuicServerConnection(options, Time, RandomNumberGenerator.Create());
        Client = UnitTestQuicClient.FromRecording(fixture, editClientHello);
    }

    public UnitTestTimeProvider Time { get; } = new();

    public QuicServerConnection Server { get; }

    public UnitTestQuicClient Client { get; }

    public long BytesToServer { get; private set; }

    public long BytesFromServer { get; private set; }

    /// <summary>Every datagram the server has sent, in order.</summary>
    public List<byte[]> ServerDatagrams { get; } = [];

    /// <summary>A connection whose handshake with the test client is complete.</summary>
    public static UnitTestExchange Completed(RSA key)
    {
        var exchange = new UnitTestExchange(UnitTestCertificates.Options(key));
        exchange.SendRecordedInitials();
        exchange.SendFinished();
        Assert.IsTrue(exchange.Server.IsHandshakeComplete);
        return exchange;
    }

    public void SendRecordedInitials()
    {
        foreach (var datagram in Client.InitialDatagrams)
        {
            Send(datagram);
        }
    }

    public void SendFinished() => Send(Client.FinishedDatagram());

    /// <summary>Gives the server one datagram and the client what the server sends back.</summary>
    public QuicConnectionOutput Send(byte[] datagram)
    {
        BytesToServer += datagram.Length;
        var output = Server.ReceiveDatagram(datagram);
        Take(output);
        return output;
    }

    public void Take(QuicConnectionOutput output)
    {
        BytesFromServer += output.Datagrams.Sum(datagram => datagram.Length);
        ServerDatagrams.AddRange(output.Datagrams);
        Client.Receive(output.Datagrams);
    }

    public void Dispose()
    {
        Server.Dispose();
        Client.Dispose();
    }
}
