using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

/// <summary>
/// What every <see cref="WsProtocolServer"/> test shares: a served root in memory, a fixed
/// clock, the exchange context, and the recorded upgrade request of upstream curl 8.21.0.
/// </summary>
internal static class WsServerHarness
{
    public static readonly string Root = Path.Join(Path.GetTempPath(), "surl-ws-tests");

    public static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The 101 the recorded upgrade request is answered with, which pinned upstream curl
    /// completed with exit 0 (Fixtures/upgrade-101/transcript.txt), then the empty CLOSE.
    /// </summary>
    public const string Recorded101Response =
        "HTTP/1.1 101 Switching Protocols\r\n"
        + "Date: Mon, 28 Sep 2026 12:00:00 GMT\r\n"
        + "Server: surl\r\n"
        + "Upgrade: websocket\r\n"
        + "Connection: Upgrade\r\n"
        + "Sec-WebSocket-Accept: ktpdlwK4CWD8HWwKyLX0kug7bZ8=\r\n"
        + "\r\n"
        + "\x88\x00";

    public static UnitTestInMemoryContentFileSystem StandardFileSystem() => new UnitTestInMemoryContentFileSystem()
        .AddDirectory(Root)
        .AddFile(Path.Join(Root, "chat"), Encoding.ASCII.GetBytes("chat"), Now)
        .AddFile(Path.Join(Root, "file.txt"), Encoding.ASCII.GetBytes("file"), Now)
        .AddDirectory(Path.Join(Root, "sub"));

    public static WsProtocolServer Server(IAuthenticationPolicy? policy = null, bool listDirectories = false) => new(
        new ContentStore(Root, StandardFileSystem(), new ContentExposureOptions { ListDirectories = listDirectories }),
        policy ?? new AnonymousAuthenticationPolicy());

    public static ExchangeContext Context(IExchangeLog log, TimeProvider timeProvider, CancellationToken cancellationToken, ExchangeLimits? limits = null) => new(
        1,
        new ListenUrl("ws", "127.0.0.1", 18301).WithBoundPort(18301),
        new IPEndPoint(IPAddress.Loopback, 18301),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        timeProvider,
        cancellationToken)
    {
        Limits = limits ?? ExchangeLimits.Default,
    };

    public static byte[] Latin1Bytes(string text) => Encoding.Latin1.GetBytes(text);

    public static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    /// <summary>
    /// The recorded upgrade request (<c>curl -sS ws://127.0.0.1:18301/chat</c>) as text.
    /// </summary>
    public static string RecordedUpgradeRequest() => Latin1(RecordedFixture.ReadRequestBytes("upgrade-101"));

    /// <summary>
    /// The recorded upgrade request with <paramref name="oldText"/> replaced, for a defect
    /// curl itself never sends.
    /// </summary>
    public static byte[] RecordedUpgradeRequestWith(string oldText, string newText)
    {
        var request = RecordedUpgradeRequest();
        Assert.Contains(oldText, request);

        return Latin1Bytes(request.Replace(oldText, newText, StringComparison.Ordinal));
    }

    /// <summary>
    /// A refusal ADR-0071 decision 1 writes: Date, Server, the refusal's own fields,
    /// Content-Length: 0 and Connection: close.
    /// </summary>
    public static string Refusal(string statusLine, params string[] fields) =>
        $"HTTP/1.1 {statusLine}\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\n"
        + string.Concat(fields.Select(field => field + "\r\n"))
        + "Content-Length: 0\r\nConnection: close\r\n\r\n";

    public static async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        IEnumerable<ReadOnlyMemory<byte>> chunks, CancellationToken cancellationToken, IAuthenticationPolicy? policy = null, ExchangeLimits? limits = null, bool listDirectories = false)
    {
        var connection = new InMemoryConnection(chunks);
        var log = new RecordingExchangeLog();

        await Server(policy, listDirectories).ServeAsync(connection, Context(log, new ManualTimeProvider(Now), cancellationToken, limits));

        return (connection, log);
    }

    /// <summary>
    /// Once the server has half-closed and its lingering close is waiting, runs the linger's
    /// one second out and waits for the exchange to end.
    /// </summary>
    public static async Task EndLingeringCloseAsync(ManualTimeProvider clock, InMemoryConnection connection, Task serving)
    {
        await WaitForLingeringCloseAsync(clock, connection);
        clock.Advance(WebSocketLingeringClose.MaxLingerTime);

        await serving;
    }

    /// <summary>
    /// Waits until the server has half-closed and its lingering close's timer runs.
    /// </summary>
    public static async Task WaitForLingeringCloseAsync(ManualTimeProvider clock, InMemoryConnection connection)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!connection.WritesCompleted && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1));
        }

        await WaitForTimersAsync(clock, 1);
    }

    public static async Task WaitForTimersAsync(ManualTimeProvider clock, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (clock.ActiveTimerCount != count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1));
        }

        Assert.AreEqual(count, clock.ActiveTimerCount);
    }
}
