using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A SignalR server for the hub <c>/chat</c> whose clock stands still, and the means to send it
/// requests written from TransportProtocols.md through <see cref="InMemoryConnection"/>.
/// </summary>
internal sealed class SignalRServerHarness
{
    /// <summary>
    /// The clock: 2026-11-05 12:00:00 UTC.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    public const string NowHttpDate = "Thu, 05 Nov 2026 12:00:00 GMT";

    public const string BearerField = "Authorization: Bearer " + UnitTestBearerAuthenticationPolicy.Token;

    public SignalRServerHarness(bool allowsAnonymous = false, string listenPath = "/chat")
    {
        Clock = new SteppingTimeProvider(Now);
        Policy = new UnitTestBearerAuthenticationPolicy(allowsAnonymous);
        Connections = new SignalRHubConnections(Clock);
        Server = new SignalRProtocolServer(SignalRHubPath.FromListenPath(listenPath), Connections, Policy);
    }

    public SteppingTimeProvider Clock { get; }

    public UnitTestBearerAuthenticationPolicy Policy { get; }

    public SignalRHubConnections Connections { get; }

    public SignalRProtocolServer Server { get; }

    public RecordingExchangeLog Log { get; } = new();

    public ExchangeContext Context() => new(
        1,
        new ListenUrl(SignalRProtocolServer.PlainScheme, "127.0.0.1", 5000).WithBoundPort(5000),
        new IPEndPoint(IPAddress.Loopback, 5000),
        new IPEndPoint(IPAddress.Loopback, 50000),
        Log,
        Clock,
        CancellationToken.None);

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

    /// <summary>
    /// The body after a response's head.
    /// </summary>
    public static string BodyOf(string response) => response[(response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
}
