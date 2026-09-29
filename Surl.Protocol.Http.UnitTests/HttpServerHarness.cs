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
    /// once the exchange is over.
    /// </summary>
    public static async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        IEnumerable<ReadOnlyMemory<byte>> chunks, CancellationToken cancellationToken, ExchangeLimits? limits = null, bool peerHalfCloses = true, IContentFileSystem? fileSystem = null)
    {
        var connection = new InMemoryConnection(chunks, peerHalfClosesWhenExhausted: peerHalfCloses);
        var log = new RecordingExchangeLog();

        await Server(fileSystem).ServeAsync(connection, Context(log, new FixedTimeProvider(Now), cancellationToken, limits));

        return (connection, log);
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
