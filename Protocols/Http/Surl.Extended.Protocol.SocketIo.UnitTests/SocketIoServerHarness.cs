using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// A Socket.IO server for the listen path <c>/admin</c> whose clock stands still, and the means to
/// send it requests written from EIO "Request path" and "Query parameters" through
/// <see cref="InMemoryConnection"/>.
/// </summary>
internal sealed class SocketIoServerHarness
{
    public const string NowHttpDate = "Thu, 05 Nov 2026 12:00:00 GMT";

    public SocketIoServerHarness(bool allowsAnonymous = false, string listenPath = "/admin")
    {
        Policy = new UnitTestPasswordPolicy(allowsAnonymous);
        Server = new SocketIoProtocolServer(SocketIoServedNamespaces.FromListenPath(listenPath), Sessions, Policy);
    }

    public ManualTimeProvider Clock { get; } = new();

    public UnitTestPasswordPolicy Policy { get; }

    public SocketIoSessions Sessions { get; } = new();

    public SocketIoProtocolServer Server { get; }

    public RecordingExchangeLog Log { get; } = new();

    public ExchangeContext Context(ExchangeLimits? limits = null, CancellationToken cancellationToken = default, CancellationToken shutdownToken = default) => new(
        1,
        new ListenUrl(SocketIoProtocolServer.PlainScheme, "127.0.0.1", 5000).WithBoundPort(5000),
        new IPEndPoint(IPAddress.Loopback, 5000),
        new IPEndPoint(IPAddress.Loopback, 50000),
        Log,
        Clock,
        cancellationToken)
    {
        Limits = limits ?? ExchangeLimits.Default,
        ShutdownToken = shutdownToken,
    };

    /// <summary>
    /// Sends one request head, <c>Host</c> and <c>Connection: close</c> added, and returns what
    /// the server wrote.
    /// </summary>
    public Task<string> SendAsync(string method, string target, params string[] fields) =>
        SendRawAsync($"{method} {target} HTTP/1.1\r\nHost: 127.0.0.1:5000\r\n{string.Concat(fields.Select(field => field + "\r\n"))}Connection: close\r\n\r\n");

    public async Task<string> SendRawAsync(string request)
    {
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(request)]);
        await Server.ServeAsync(connection, Context());

        return Encoding.UTF8.GetString(connection.WrittenBytes);
    }
}
