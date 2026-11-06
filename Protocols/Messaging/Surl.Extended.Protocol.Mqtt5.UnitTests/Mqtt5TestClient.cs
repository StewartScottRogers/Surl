using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// One client connected to a broker the test runs: it has sent its <c>CONNECT</c> and read the
/// <c>CONNACK</c>, and sends and reads packets while the broker serves it.
/// </summary>
internal sealed class Mqtt5TestClient
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private readonly Mqtt5TestPipeConnection connection;
    private readonly CancellationToken cancellationToken;

    private Mqtt5TestClient(Mqtt5TestPipeConnection connection, Task serving, byte[] connectAcknowledgement, CancellationToken cancellationToken)
    {
        this.connection = connection;
        this.cancellationToken = cancellationToken;
        Serving = serving;
        ConnectAcknowledgement = connectAcknowledgement;
    }

    /// <summary>The broker's serving of the connection.</summary>
    public Task Serving { get; }

    /// <summary>The <c>CONNACK</c> the broker answered with.</summary>
    public byte[] ConnectAcknowledgement { get; }

    /// <summary>
    /// Connects to <paramref name="broker"/> with <paramref name="connect"/> under <paramref name="context"/>.
    /// </summary>
    public static async Task<Mqtt5TestClient> ConnectAsync(Mqtt5Broker broker, ExchangeContext context, byte[] connect)
    {
        var connection = new Mqtt5TestPipeConnection();
        var serving = broker.ServeAsync(connection, context);
        connection.Send(connect);
        var connectAcknowledgement = await ReadAsync(connection, context.CancellationToken);
        return new(connection, serving, connectAcknowledgement, context.CancellationToken);
    }

    /// <summary>Sends <paramref name="packets"/>, in order.</summary>
    public void Send(params byte[][] packets)
    {
        foreach (var packet in packets)
        {
            connection.Send(packet);
        }
    }

    /// <summary>Sends <paramref name="packet"/>, encoded.</summary>
    public void Send(Mqtt5Packet packet) => connection.Send(Mqtt5PacketEncoder.Encode(packet));

    /// <summary>Waits for the broker's next packet.</summary>
    public Task<byte[]> ReadPacketAsync() => ReadAsync(connection, cancellationToken);

    /// <summary>Waits for the broker's next packet and decodes it.</summary>
    public async Task<Mqtt5Packet> ReadDecodedPacketAsync()
    {
        var bytes = await ReadPacketAsync();
        return Mqtt5PacketDecoder.Decode(bytes[0], bytes.AsMemory(RemainingLengthEnd(bytes))).Packet!;
    }

    /// <summary>
    /// Sends <c>DISCONNECT</c> <c>0x00</c>, waits for the serving to end, and returns every byte the
    /// broker wrote that was not read.
    /// </summary>
    public async Task<byte[]> DisconnectAsync()
    {
        connection.Send(Mqtt5BrokerExchange.DisconnectNormally);
        await Serving;
        return connection.UnreadServerBytes();
    }

    /// <summary>Ends the connection from the client's side with no <c>DISCONNECT</c>, as a network loss would.</summary>
    public void Abort() => connection.Abort();

    /// <summary>Waits for the serving to end, the client sending nothing more, and returns every unread byte.</summary>
    public async Task<byte[]> ServingEndedAsync()
    {
        await Serving;
        return connection.UnreadServerBytes();
    }

    private static int RemainingLengthEnd(byte[] packet)
    {
        var index = 1;
        while ((packet[index] & 0x80) != 0)
        {
            index++;
        }

        return index + 1;
    }

    private static async Task<byte[]> ReadAsync(Mqtt5TestPipeConnection connection, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadTimeout);
        return await connection.ReadPacketAsync(timeout.Token);
    }
}
