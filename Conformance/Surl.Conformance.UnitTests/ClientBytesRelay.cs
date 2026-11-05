using System.Net;
using System.Net.Sockets;

namespace Surl.Conformance;

/// <summary>
/// A loopback relay between a client and <c>surl</c> that records, in a <see cref="ClientByteTranscript"/>,
/// the bytes the client sends on each connection, in the order it accepts them (ADR-0089 decision 4,
/// BL-579). Both directions pass through unchanged; only client-to-server bytes are recorded. When either
/// side stops sending, the relay shuts down sending to the other, and a connection ends once both
/// directions have. Connections are relayed concurrently. The client is pointed at <see cref="Port"/>, the
/// relay at <c>surl</c>'s port; a connection the client opens elsewhere (an FTP data connection to the port
/// <c>surl</c> announced) does not pass through it. Disposing it stops it.
/// </summary>
internal sealed class ClientBytesRelay : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly IPEndPoint surl;
    private readonly CancellationTokenSource stop;
    private readonly List<Task> connections = [];
    private readonly Lock connectionsGate = new();
    private readonly Task accepting;

    private ClientBytesRelay(IPEndPoint surl, CancellationToken cancellationToken)
    {
        this.surl = surl;
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        accepting = AcceptConnectionsAsync(stop.Token);
    }

    /// <summary>Gets the relay's own port on <c>127.0.0.1</c>.</summary>
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    /// <summary>Gets what the client has sent so far, one entry per connection.</summary>
    public ClientByteTranscript Transcript { get; } = new();

    /// <summary>
    /// Starts a relay to the <c>surl</c> listening at <paramref name="surlBaseUrl"/>.
    /// </summary>
    public static ClientBytesRelay Start(Uri surlBaseUrl, CancellationToken cancellationToken) =>
        new(new IPEndPoint(IPAddress.Parse(surlBaseUrl.Host.Trim('[', ']')), surlBaseUrl.Port), cancellationToken);

    /// <summary>Gets <paramref name="surlBaseUrl"/> with its port replaced by the relay's.</summary>
    public Uri BaseUrlFor(Uri surlBaseUrl) => new UriBuilder(surlBaseUrl) { Port = Port }.Uri;

    /// <summary>
    /// Waits for every connection accepted so far to finish relaying - each ends when both its sides
    /// have stopped sending - so the transcript is complete once the client has exited.
    /// </summary>
    public async Task WaitForConnectionsAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        Task[] open;
        lock (connectionsGate)
        {
            open = [.. connections];
        }

        await Task.WhenAll(open).WaitAsync(timeout, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        listener.Stop();
        await IgnoringStopAsync(accepting);
        Task[] open;
        lock (connectionsGate)
        {
            open = [.. connections];
        }

        foreach (var connection in open)
        {
            await IgnoringStopAsync(connection);
        }

        stop.Dispose();
    }

    private static async Task IgnoringStopAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken);
            var index = Transcript.BeginConnection();
            lock (connectionsGate)
            {
                connections.Add(RelayConnectionAsync(client, index, cancellationToken));
            }
        }
    }

    private async Task RelayConnectionAsync(TcpClient client, int index, CancellationToken cancellationToken)
    {
        using (client)
        using (var server = new TcpClient())
        {
            await server.ConnectAsync(surl, cancellationToken);
            var toServer = CopyAsync(client.Client, server.Client, index, cancellationToken);
            var toClient = CopyAsync(server.Client, client.Client, null, cancellationToken);
            await Task.WhenAll(toServer, toClient);
        }
    }

    // Copies one direction until its sender stops sending or resets, recording what passes when
    // given a connection index, then stops sending to the receiver.
    private async Task CopyAsync(Socket from, Socket to, int? recordedConnection, CancellationToken cancellationToken)
    {
        var buffer = new byte[16384];
        try
        {
            int read;
            while ((read = await from.ReceiveAsync(buffer, SocketFlags.None, cancellationToken)) > 0)
            {
                if (recordedConnection is { } connection)
                {
                    Transcript.Record(connection, buffer.AsSpan(0, read));
                }

                await to.SendAsync(buffer.AsMemory(0, read), SocketFlags.None, cancellationToken);
            }
        }
        catch (SocketException)
        {
        }

        try
        {
            to.Shutdown(SocketShutdown.Send);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
