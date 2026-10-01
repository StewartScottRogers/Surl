using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Rtsp;

/// <summary>
/// What the RTSP server's tests share: a content store, the recorded clock, an exchange context
/// with chosen limits, and the fixtures recorded from pinned upstream curl.
/// </summary>
internal static class RtspServerHarness
{
    public const string ClipBody = "clip bytes";

    public static readonly string Root = Path.Join(Path.GetTempPath(), "surl-rtsp-tests");

    /// <summary>
    /// The clock every recorded response's <c>Date</c> was taken from: 2026-09-28 12:00:00 UTC.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    public const string DateField = "Date: Mon, 28 Sep 2026 12:00:00 GMT\r\n";

    /// <summary>
    /// <c>long.bin</c>s bytes: 50 full RTP payloads and 100 bytes more, so it streams as 51 packets.
    /// </summary>
    public static readonly byte[] LongClip = Enumerable.Range(0, (50 * RtspSession.PayloadBytes) + 100).Select(index => (byte)(index % 251)).ToArray();

    /// <summary>
    /// <c>clip.bin</c>, <c>.hidden</c> and <c>fizzle/foo</c> (RFC 2326 section 10.2's example
    /// presentation), <c>long.bin</c> and the empty <c>empty.bin</c> as files, and <c>fizzle</c> and <c>media</c> as directories.
    /// </summary>
    public static UnitTestInMemoryContentFileSystem StandardFileSystem() => new UnitTestInMemoryContentFileSystem()
        .AddDirectory(Root)
        .AddFile(Path.Join(Root, "clip.bin"), Ascii(ClipBody), Now)
        .AddFile(Path.Join(Root, ".hidden"), Ascii(ClipBody), Now)
        .AddDirectory(Path.Join(Root, "fizzle"))
        .AddFile(Path.Join(Root, "fizzle", "foo"), Ascii(ClipBody), Now)
        .AddDirectory(Path.Join(Root, "media"))
        .AddFile(Path.Join(Root, "long.bin"), LongClip, Now)
        .AddFile(Path.Join(Root, "empty.bin"), [], Now);

    /// <summary>
    /// A server over <paramref name="fileSystem"/> (the standard one when none is given) that
    /// lets in what <paramref name="authenticationPolicy"/> does, everyone when none is given.
    /// </summary>
    public static RtspProtocolServer Server(IContentFileSystem? fileSystem = null, IAuthenticationPolicy? authenticationPolicy = null) =>
        new(new ContentStore(Root, fileSystem ?? StandardFileSystem(), new ContentExposureOptions()), authenticationPolicy ?? new AnonymousAuthenticationPolicy(), new PatternRandomNumberGenerator());

    public static ExchangeContext Context(IExchangeLog log, TimeProvider timeProvider, CancellationToken cancellationToken, ExchangeLimits? limits = null) => new(
        1,
        new ListenUrl("rtsp", "127.0.0.1", 18554).WithBoundPort(18554),
        new IPEndPoint(IPAddress.Loopback, 18554),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        timeProvider,
        cancellationToken)
    {
        Limits = limits ?? ExchangeLimits.Default,
    };

    public static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    public static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    public static byte[] RecordedRequest(string caseName) => ReadFixture(caseName, "request.bin");

    public static byte[] RecordedResponse(string caseName) => ReadFixture(caseName, "response.bin");

    public static IEnumerable<ReadOnlyMemory<byte>> OneBytePerRead(byte[] bytes) =>
        Enumerable.Range(0, bytes.Length).Select(index => new ReadOnlyMemory<byte>(bytes, index, 1));

    /// <summary>
    /// A response head with the recorded clock's <c>Date</c> and <c>Server: surl</c>, the
    /// <c>CSeq</c> first when there is one, then <paramref name="fields"/>.
    /// </summary>
    public static string ResponseHead(string statusLine, string? cseq, params string[] fields)
    {
        var text = new StringBuilder("RTSP/1.0 ").Append(statusLine).Append("\r\n");
        if (cseq is not null)
        {
            text.Append("CSeq: ").Append(cseq).Append("\r\n");
        }

        text.Append(DateField).Append("Server: surl\r\n");
        foreach (var field in fields)
        {
            text.Append(field).Append("\r\n");
        }

        return text.Append("\r\n").ToString();
    }

    /// <summary>
    /// Serves <paramref name="chunks"/> with the recorded clock standing still, and returns once
    /// the exchange is over. When the peer never half-closes, the exchange is expected to end in
    /// a refusal's drain, so the clock is moved on past the drain's time limit.
    /// </summary>
    public static async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        IEnumerable<ReadOnlyMemory<byte>> chunks,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        bool peerHalfCloses = true,
        IContentFileSystem? fileSystem = null,
        EndPoint? localEndPoint = null,
        IAuthenticationPolicy? authenticationPolicy = null)
    {
        var connection = new InMemoryConnection(chunks, localEndPoint, peerHalfClosesWhenExhausted: peerHalfCloses);
        var log = new RecordingExchangeLog();
        var clock = new ManualTimeProvider(Now);

        var serving = Server(fileSystem, authenticationPolicy).ServeAsync(connection, Context(log, clock, cancellationToken, limits));
        await (peerHalfCloses ? serving : AdvanceUntilCompletedAsync(clock, serving));

        return (connection, log);
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
    /// Waits for the server's drain timer, fires it by moving <paramref name="clock"/> on by the
    /// drain's time limit, and awaits <paramref name="serving"/>.
    /// </summary>
    public static async Task AdvanceUntilCompletedAsync(ManualTimeProvider clock, Task serving)
    {
        await WaitForTimersAsync(clock, 1);
        clock.Advance(RtspUnreadRequestDrainer.MaxDrainTime);

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

    private static byte[] ReadFixture(string caseName, string fileName)
    {
        using var stream = typeof(RtspServerHarness).Assembly.GetManifestResourceStream($"Fixtures/{caseName}/{fileName}")
            ?? throw new InvalidOperationException($"No embedded fixture Fixtures/{caseName}/{fileName}.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }
}
