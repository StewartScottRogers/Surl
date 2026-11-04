using System.Net;

namespace Surl.Networking;

/// <summary>
/// A <see cref="DataTransport"/> over an <see cref="InMemoryDuplexStream"/> pair, with the client's
/// end for the test to read and write, and a transport control that counts resets.
/// </summary>
internal sealed class InMemoryDataTransport
{
    private InMemoryDataTransport(DataTransport transport, InMemoryDuplexStream client, CountingTransportControl control)
    {
        Transport = transport;
        Client = client;
        Control = control;
    }

    public DataTransport Transport { get; }

    public InMemoryDuplexStream Client { get; }

    public CountingTransportControl Control { get; }

    public static InMemoryDataTransport Create(EndPoint local, EndPoint remote)
    {
        var (server, client) = InMemoryDuplexStream.CreatePair();
        var control = new CountingTransportControl(server);

        return new InMemoryDataTransport(new DataTransport(server, local, remote, control), client, control);
    }

    public sealed class CountingTransportControl(InMemoryDuplexStream stream) : IConnectionTransportControl
    {
        public int ShutdownSendCount { get; private set; }

        public int ResetAndCloseCount { get; private set; }

        public void ShutdownSend()
        {
            ShutdownSendCount++;
            stream.CompleteWrites();
        }

        public void ResetAndClose()
        {
            ResetAndCloseCount++;
            stream.Dispose();
        }
    }
}
