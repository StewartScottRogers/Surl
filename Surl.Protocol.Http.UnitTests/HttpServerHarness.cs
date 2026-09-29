using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// What the hardening tests share: a content store holding <c>file.txt</c>, the recorded
/// clock, and an exchange context with chosen limits.
/// </summary>
internal static class HttpServerHarness
{
    public const string FileBody = "Hello from Surl.\n";

    public static readonly string Root = Path.Join(Path.GetTempPath(), "surl-http-tests");

    /// <summary>
    /// The clock every recorded response's <c>Date</c> was taken from: 2026-09-28 12:00:00 UTC.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset FileTime = new(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);

    public static InMemoryContentFileSystem StandardFileSystem() => new InMemoryContentFileSystem()
        .AddDirectory(Root)
        .AddFile(Path.Join(Root, "file.txt"), Encoding.ASCII.GetBytes(FileBody), FileTime);

    public static HttpProtocolServer Server(IContentFileSystem? fileSystem = null) =>
        new(new ContentStore(Root, fileSystem ?? StandardFileSystem()));

    public static ExchangeContext Context(IExchangeLog log, TimeProvider timeProvider, CancellationToken cancellationToken, ExchangeLimits? limits = null) => new(
        1,
        new ListenUrl("http", "127.0.0.1", 18050).WithBoundPort(18050),
        new IPEndPoint(IPAddress.Loopback, 18050),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        timeProvider,
        cancellationToken)
    {
        Limits = limits ?? ExchangeLimits.Default,
    };

    public static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    public static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    public static byte[] RecordedResponse(string caseName) => RecordedFixture.ReadBytes(caseName, "response.bin");

    /// <summary>
    /// Serves <paramref name="chunks"/> with the recorded clock standing still, and returns
    /// once the exchange is over. When the peer never half-closes, the exchange ends in the
    /// drain before the close, so the clock is moved on past the drain's time limit.
    /// </summary>
    public static async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        IEnumerable<ReadOnlyMemory<byte>> chunks, CancellationToken cancellationToken, ExchangeLimits? limits = null, bool peerHalfCloses = true, IContentFileSystem? fileSystem = null)
    {
        var connection = new InMemoryConnection(chunks, peerHalfClosesWhenExhausted: peerHalfCloses);
        var log = new RecordingExchangeLog();
        var clock = new ManualTimeProvider(Now);

        var serving = Server(fileSystem).ServeAsync(connection, Context(log, clock, cancellationToken, limits));
        await (peerHalfCloses ? serving : AdvanceUntilCompletedAsync(clock, serving));

        return (connection, log);
    }

    /// <summary>
    /// Serves <paramref name="chunks"/> to a client that half-closes once they are sent, and
    /// says how many bytes the server had read when it first wrote: what it read to answer,
    /// before the drain before the close read the rest.
    /// </summary>
    public static async Task<(InMemoryConnection Connection, long BytesReadBeforeAnswer)> ServeCountingReadsAsync(
        IEnumerable<ReadOnlyMemory<byte>> chunks, CancellationToken cancellationToken, ExchangeLimits? limits = null)
    {
        var connection = new InMemoryConnection(chunks);
        var counting = new ReadCountingConnection(connection);

        await Server().ServeAsync(counting, Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), cancellationToken, limits));

        return (connection, counting.BytesReadBeforeFirstWrite);
    }

    /// <summary>
    /// Waits, for at most ten seconds of real time, until <paramref name="clock"/> holds
    /// <paramref name="count"/> live timers, so a test knows the timer it is about to fire exists.
    /// </summary>
    public static async Task WaitForTimersAsync(ManualTimeProvider clock, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (clock.ActiveTimerCount != count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1));
        }

        Assert.AreEqual(count, clock.ActiveTimerCount);
    }

    /// <summary>
    /// Waits for the server's drain timer, fires it by moving <paramref name="clock"/> on by
    /// the drain's time limit, and awaits <paramref name="serving"/>.
    /// </summary>
    public static async Task AdvanceUntilCompletedAsync(ManualTimeProvider clock, Task serving)
    {
        await WaitForTimersAsync(clock, 1);
        clock.Advance(HttpUnreadRequestDrainer.MaxDrainTime);

        await serving;
    }

    /// <summary>
    /// Takes what the server left unread on <paramref name="connection"/>, up to 64 KiB.
    /// </summary>
    public static async Task<byte[]> ReadWhatIsLeftAsync(InMemoryConnection connection, CancellationToken cancellationToken)
    {
        var rest = new byte[64 * 1024];
        var count = await connection.ReadAsync(rest, cancellationToken);

        return rest[..count];
    }
}
