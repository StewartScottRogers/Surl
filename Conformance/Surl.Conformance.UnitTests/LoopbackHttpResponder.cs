using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// A loopback HTTP/1.1 listener for probing what the pinned upstream curl build sends: it records
/// each request head (and the client port it came from) and answers with what its answer function
/// returns for that head. Connections are served concurrently and each is kept alive until the
/// client closes it; a request body is read by its <c>Content-Length</c> and dropped. Only for
/// heads and bodies curl sends to a probe, never a general HTTP server. Disposing it stops it.
/// </summary>
internal sealed class LoopbackHttpResponder : IAsyncDisposable
{
    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();

    private readonly TcpListener listener;
    private readonly Func<string, string> answer;
    private readonly CancellationTokenSource stop;
    private readonly List<Task> connections = [];
    private readonly List<LoopbackHttpRequest> requests = [];
    private readonly Lock gate = new();
    private readonly Task accepting;

    private LoopbackHttpResponder(Func<string, string> answer, CancellationToken cancellationToken)
    {
        this.answer = answer;
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        accepting = AcceptConnectionsAsync(stop.Token);
    }

    /// <summary>Gets the URL of <paramref name="path"/> on this responder.</summary>
    public string UrlOf(string path) => $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/{path}";

    /// <summary>Gets the requests recorded so far, in the order their heads were read.</summary>
    public IReadOnlyList<LoopbackHttpRequest> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    /// <summary>
    /// Starts a responder that answers every request with what <paramref name="answer"/> returns
    /// for its head: a whole HTTP/1.1 response, with its own <c>Content-Length</c>.
    /// </summary>
    public static LoopbackHttpResponder Start(Func<string, string> answer, CancellationToken cancellationToken) =>
        new(answer, cancellationToken);

    /// <summary>A <c>200 OK</c> carrying <paramref name="body"/> and any extra header lines.</summary>
    public static string Ok(string body, params string[] headerLines) =>
        "HTTP/1.1 200 OK\r\n" + string.Concat(headerLines.Select(line => line + "\r\n"))
        + $"Content-Length: {Encoding.ASCII.GetByteCount(body)}\r\n\r\n{body}";

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        listener.Stop();
        await IgnoringStopAsync(accepting);
        Task[] open;
        lock (gate)
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
        catch (IOException)
        {
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken);
            lock (gate)
            {
                connections.Add(ServeConnectionAsync(client, cancellationToken));
            }
        }
    }

    private async Task ServeConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            var stream = client.GetStream();
            var clientPort = ((IPEndPoint)client.Client.RemoteEndPoint!).Port;
            var buffered = new List<byte>();
            while (await ReadHeadAsync(stream, buffered, cancellationToken) is { } head)
            {
                await DropBodyAsync(stream, buffered, ContentLengthOf(head), cancellationToken);
                lock (gate)
                {
                    requests.Add(new LoopbackHttpRequest(clientPort, head));
                }

                await stream.WriteAsync(Encoding.ASCII.GetBytes(answer(head)), cancellationToken);
            }
        }
    }

    // Reads until the buffered bytes hold a whole head, returns it and leaves what follows it
    // buffered; null once the client closes before sending one.
    private static async Task<string?> ReadHeadAsync(NetworkStream stream, List<byte> buffered, CancellationToken cancellationToken)
    {
        var chunk = new byte[4096];
        int end;
        while ((end = buffered.ToArray().AsSpan().IndexOf(HeadEnd)) < 0)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return null;
            }

            buffered.AddRange(chunk.AsSpan(0, read));
        }

        var head = Encoding.Latin1.GetString(buffered.ToArray(), 0, end + HeadEnd.Length);
        buffered.RemoveRange(0, end + HeadEnd.Length);
        return head;
    }

    private static async Task DropBodyAsync(NetworkStream stream, List<byte> buffered, int length, CancellationToken cancellationToken)
    {
        var chunk = new byte[4096];
        while (buffered.Count < length)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return;
            }

            buffered.AddRange(chunk.AsSpan(0, read));
        }

        buffered.RemoveRange(0, length);
    }

    private static int ContentLengthOf(string head)
    {
        const string Name = "content-length:";
        var line = head.Split("\r\n").FirstOrDefault(field => field.StartsWith(Name, StringComparison.OrdinalIgnoreCase));
        return line is null ? 0 : int.Parse(line[Name.Length..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
