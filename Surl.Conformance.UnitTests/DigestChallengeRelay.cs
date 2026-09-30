using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// A loopback relay in front of an <c>http</c> surl that passes every byte through unchanged
/// except that it drops, from each response head, every <c>WWW-Authenticate: Digest</c> field
/// whose algorithm is not the one kept. surl offers <c>MD5</c> first (ADR-0032, section 4) and
/// curl answers only the first <c>Digest</c> challenge, so this is how a test makes the pinned
/// build answer <c>SHA-256</c> or <c>SHA-512-256</c> to surl's own challenge and nonce. It
/// relays one connection at a time and reads responses by <c>Content-Length</c>, which is all
/// surl's <c>401</c> and a served file use. Disposing it stops it.
/// </summary>
internal sealed class DigestChallengeRelay : IAsyncDisposable
{
    private const string DigestChallengePrefix = "WWW-Authenticate: Digest ";
    private const string ContentLengthPrefix = "Content-Length:";

    private readonly TcpListener listener;
    private readonly Uri upstream;
    private readonly string keptAlgorithm;
    private readonly CancellationTokenSource stop;
    private readonly Task relaying;

    private DigestChallengeRelay(Uri upstream, string keptAlgorithm, CancellationToken cancellationToken)
    {
        this.upstream = upstream;
        this.keptAlgorithm = keptAlgorithm;
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        relaying = RelayConnectionsAsync(stop.Token);
    }

    /// <summary>Gets the relay's own port on <c>127.0.0.1</c>.</summary>
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    /// <summary>
    /// Starts a relay to the surl at <paramref name="upstream"/> that keeps only the
    /// <c>Digest</c> challenges with <c>algorithm=<paramref name="keptAlgorithm"/></c>.
    /// </summary>
    public static DigestChallengeRelay Start(Uri upstream, string keptAlgorithm, CancellationToken cancellationToken) =>
        new(upstream, keptAlgorithm, cancellationToken);

    /// <summary>Gets the URL of <paramref name="relativePath"/> through the relay.</summary>
    public string UrlOf(string relativePath) => $"http://127.0.0.1:{Port}/{relativePath}";

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        listener.Stop();
        try
        {
            await relaying;
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

        stop.Dispose();
    }

    private async Task RelayConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            using var server = new TcpClient();
            await server.ConnectAsync(upstream.Host, upstream.Port, cancellationToken);
            var clientStream = client.GetStream();
            var serverStream = server.GetStream();
            var requests = CopyRequestsAsync(clientStream, server.Client, cancellationToken);
            await CopyResponsesAsync(serverStream, clientStream, cancellationToken);
            client.Client.Shutdown(SocketShutdown.Send);
            await requests.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    private static async Task CopyRequestsAsync(NetworkStream client, Socket server, CancellationToken cancellationToken)
    {
        try
        {
            await client.CopyToAsync(new NetworkStream(server, ownsSocket: false), cancellationToken);
            server.Shutdown(SocketShutdown.Send);
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    // Reads each response head up to its blank line, drops the Digest challenges not kept, and
    // passes on the head and then Content-Length bytes of body, until surl closes.
    private async Task CopyResponsesAsync(NetworkStream server, NetworkStream client, CancellationToken cancellationToken)
    {
        var reader = new BufferedStreamReader(server);
        while (await reader.ReadHeadAsync(cancellationToken) is { } head)
        {
            var lines = head.Split("\r\n");
            var kept = lines.Where(line => !IsDroppedChallenge(line)).ToArray();
            await client.WriteAsync(Encoding.Latin1.GetBytes(string.Join("\r\n", kept)), cancellationToken);
            var contentLength = kept
                .Where(line => line.StartsWith(ContentLengthPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(line => long.Parse(line[ContentLengthPrefix.Length..].Trim(), System.Globalization.CultureInfo.InvariantCulture))
                .FirstOrDefault();
            await reader.CopyBytesAsync(contentLength, client, cancellationToken);
        }
    }

    private bool IsDroppedChallenge(string line) =>
        line.StartsWith(DigestChallengePrefix, StringComparison.OrdinalIgnoreCase)
        && !line.Contains($"algorithm={keptAlgorithm},", StringComparison.Ordinal)
        && !line.EndsWith($"algorithm={keptAlgorithm}", StringComparison.Ordinal);

    /// <summary>Reads response heads and bodies from a stream, keeping what it read ahead.</summary>
    private sealed class BufferedStreamReader(Stream stream)
    {
        private readonly List<byte> pending = [];

        // Returns the head through its closing CRLFCRLF, or null once the stream ends.
        public async Task<string?> ReadHeadAsync(CancellationToken cancellationToken)
        {
            int end;
            while ((end = IndexOfBlankLine()) < 0)
            {
                if (!await FillAsync(cancellationToken))
                {
                    return null;
                }
            }

            var head = Encoding.Latin1.GetString(pending.GetRange(0, end + 4).ToArray());
            pending.RemoveRange(0, end + 4);
            return head;
        }

        public async Task CopyBytesAsync(long count, Stream destination, CancellationToken cancellationToken)
        {
            while (pending.Count < count && await FillAsync(cancellationToken))
            {
            }

            var taken = (int)Math.Min(count, pending.Count);
            await destination.WriteAsync(pending.GetRange(0, taken).ToArray(), cancellationToken);
            pending.RemoveRange(0, taken);
        }

        private int IndexOfBlankLine()
        {
            for (var index = 0; index + 3 < pending.Count; index++)
            {
                if (pending[index] == '\r' && pending[index + 1] == '\n' && pending[index + 2] == '\r' && pending[index + 3] == '\n')
                {
                    return index;
                }
            }

            return -1;
        }

        private async Task<bool> FillAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer, cancellationToken);
            pending.AddRange(buffer.AsSpan(0, read));
            return read > 0;
        }
    }
}
