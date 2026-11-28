using System.Net;
using System.Text;
using System.Threading.Channels;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Redis.RedisTestExchanges;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A client that stays connected and sends one command at a time, so a test can change the server
/// between two commands of one connection. Disposing it half-closes and waits for the server to end.
/// </summary>
internal sealed class ConversationalRedisClient : IConnection, IAsyncDisposable
{
    private readonly Channel<byte[]> requests = Channel.CreateUnbounded<byte[]>();
    private readonly Channel<byte[]> replies = Channel.CreateUnbounded<byte[]>();
    private CancellationToken testToken;
    private Task serving = Task.CompletedTask;

    private ConversationalRedisClient()
    {
    }

    /// <inheritdoc/>
    public TlsSession? TlsSession => null;

    /// <inheritdoc/>
    public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 6379);

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

    /// <summary>
    /// Connects to <paramref name="server"/>, whose clock is <paramref name="clock"/> and whose
    /// exchange publishes to <paramref name="events"/>.
    /// </summary>
    public static ConversationalRedisClient Connect(RedisServer server, TimeProvider clock, ISurlEventPublisher events, CancellationToken cancellationToken)
    {
        var client = new ConversationalRedisClient { testToken = cancellationToken };
        client.serving = client.ServeThenEndRepliesAsync(server, Context(cancellationToken, clock) with { Events = events });
        return client;
    }

    /// <summary>Sends one command and asserts that the server's next bytes are <paramref name="reply"/>.</summary>
    public async Task ExpectAsync(string reply, params string[] command)
    {
        await requests.Writer.WriteAsync(Encoding.Latin1.GetBytes(Command(command)), testToken);
        var received = new StringBuilder();
        while (IsUnfinished(received.ToString(), reply) && await replies.Reader.WaitToReadAsync(testToken))
        {
            received.Append(Encoding.Latin1.GetString(await replies.Reader.ReadAsync(testToken)));
        }

        Assert.AreEqual(reply, received.ToString());
    }

    /// <inheritdoc/>
    /// <remarks>Each command is shorter than the server's read buffer, so one read takes all of it.</remarks>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!await requests.Reader.WaitToReadAsync(cancellationToken))
        {
            return 0;
        }

        var request = await requests.Reader.ReadAsync(cancellationToken);
        request.CopyTo(buffer);
        return request.Length;
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
        replies.Writer.WriteAsync(bytes.ToArray(), cancellationToken);

    /// <inheritdoc/>
    public ValueTask CompleteWritesAsync(CancellationToken cancellationToken)
    {
        replies.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<TlsSession> UpgradeToTlsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public void Abort() => requests.Writer.TryComplete();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        requests.Writer.TryComplete();
        await serving;
    }

    /// <summary>Whether <paramref name="received"/> is the start of <paramref name="reply"/> with more to come.</summary>
    private static bool IsUnfinished(string received, string reply) =>
        received.Length < reply.Length && reply.StartsWith(received, StringComparison.Ordinal);

    private async Task ServeThenEndRepliesAsync(RedisServer server, ExchangeContext context)
    {
        try
        {
            await server.ServeAsync(this, context);
        }
        finally
        {
            replies.Writer.TryComplete();
        }
    }
}
