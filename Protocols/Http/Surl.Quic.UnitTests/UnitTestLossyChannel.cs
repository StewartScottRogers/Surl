namespace Surl.Quic;

/// <summary>
/// The path between the test client and the server connection of an exchange, dropping every
/// twentieth datagram in each direction - 5% of the packets, chosen deterministically.
/// </summary>
internal sealed class UnitTestLossyChannel(UnitTestExchange exchange)
{
    public const int DropEvery = 20;

    private int toServer;
    private int toClient;

    /// <summary>How many datagrams the channel has dropped, both ways.</summary>
    public int Dropped { get; private set; }

    /// <summary>Gives the server the datagram unless it is dropped, and the client what the server sends back, less what is dropped.</summary>
    /// <returns>What the server produced; nothing when the datagram was dropped.</returns>
    public QuicConnectionOutput SendToServer(byte[] datagram)
    {
        if (++toServer % DropEvery == 0)
        {
            Dropped++;
            return QuicConnectionOutput.Empty;
        }

        var output = exchange.Server.ReceiveDatagram(datagram);
        Deliver(output);
        return output;
    }

    /// <summary>Gives the client the server's datagrams, less what is dropped.</summary>
    public void Deliver(QuicConnectionOutput output)
    {
        foreach (var datagram in output.Datagrams)
        {
            if (++toClient % DropEvery == 0)
            {
                Dropped++;
                continue;
            }

            exchange.Client.Receive([datagram]);
        }
    }

    /// <summary>Moves the clock to the server's next deadline - its loss detection timer here - and delivers what it sends then.</summary>
    public void AdvanceToNextDeadline()
    {
        Assert.IsNotNull(exchange.Server.NextDeadline, "The server's connection has ended.");
        exchange.Time.Advance(exchange.Server.NextDeadline!.Value - exchange.Time.GetUtcNow());
        Deliver(exchange.Server.HandleTimeout());
    }
}
