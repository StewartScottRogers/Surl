using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Gopher;

/// <summary>
/// Builds the server, content and exchange the limit and exposure tests share.
/// </summary>
internal static class GopherTestExchange
{
    public const string FileBody = "Hello from Surl.\n";

    public static readonly string Root = Path.Join(Path.GetTempPath(), "surl-gopher-limit-tests");

    private static readonly DateTimeOffset FileTime = new(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// A root holding <c>file.txt</c>, <c>.hidden.txt</c>, the directory <c>sub</c> and the
    /// dot-directory <c>.git</c>.
    /// </summary>
    public static InMemoryContentFileSystem FileSystemWithDotFiles() => new InMemoryContentFileSystem()
        .AddDirectory(Root)
        .AddDirectory(Path.Join(Root, "sub"))
        .AddDirectory(Path.Join(Root, ".git"))
        .AddFile(Path.Join(Root, "file.txt"), Encoding.ASCII.GetBytes(FileBody), FileTime)
        .AddFile(Path.Join(Root, ".hidden.txt"), "secret\n"u8.ToArray(), FileTime);

    /// <summary>
    /// A server over <see cref="FileSystemWithDotFiles"/> with <paramref name="exposureOptions"/>,
    /// ADR-0006's defaults when none are given.
    /// </summary>
    public static GopherProtocolServer Server(ContentExposureOptions? exposureOptions = null) =>
        new(new ContentStore(Root, FileSystemWithDotFiles(), exposureOptions ?? new ContentExposureOptions()));

    public static ExchangeContext Context(
        TimeProvider clock,
        IExchangeLog log,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null) => new(
        1,
        new ListenUrl("gopher", "127.0.0.1", 18634).WithBoundPort(18634),
        new IPEndPoint(IPAddress.Loopback, 18634),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        clock,
        cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };

    /// <summary>
    /// Serves <paramref name="request"/>, whole, to a client that half-closes after it.
    /// </summary>
    public static async Task<(InMemoryConnection Connection, RecordingExchangeLog Log)> ServeAsync(
        GopherProtocolServer server, byte[] request, CancellationToken cancellationToken)
    {
        var connection = new InMemoryConnection(RecordedFixture.Whole(request));
        var log = new RecordingExchangeLog();

        await server.ServeAsync(connection, Context(TimeProvider.System, log, cancellationToken));

        return (connection, log);
    }

    public static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    public static string Utf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
