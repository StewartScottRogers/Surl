using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Dict;

/// <summary>
/// What the limit tests share: a DICT server over <c>hello</c>, <c>help</c> and <c>world</c>,
/// and an exchange context on a clock the test controls.
/// </summary>
internal static class DictTestExchange
{
    public const string Banner = "220 surl DICT server <mime> <1@surl>\r\n";
    public const string HeadTimedOutReply = "420 timed out waiting for a command\r\n";
    public const string LineTooLongReply = "500 line too long\r\n";

    public static readonly string Root = Path.Join(Path.GetTempPath(), "surl-dict-tests");

    private static readonly DateTimeOffset FileTime = new(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);

    public static InMemoryContentFileSystem StandardFileSystem() => new InMemoryContentFileSystem()
        .AddDirectory(Root)
        .AddFile(Path.Join(Root, "hello"), "A greeting.\n"u8.ToArray(), FileTime)
        .AddFile(Path.Join(Root, "help"), "Assistance.\n"u8.ToArray(), FileTime)
        .AddFile(Path.Join(Root, "world"), "The earth.\n"u8.ToArray(), FileTime);

    public static DictProtocolServer Server(InMemoryContentFileSystem? fileSystem = null) =>
        new(new ContentStore(Root, fileSystem ?? StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot));

    public static ExchangeContext Context(
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null,
        long exchangeId = 1) => new(
            exchangeId,
            new ListenUrl("dict", "127.0.0.1", 18628).WithBoundPort(18628),
            new IPEndPoint(IPAddress.Loopback, 18628),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider,
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };

    public static IEnumerable<ReadOnlyMemory<byte>> Ascii(params string[] chunks) =>
        chunks.Select(chunk => new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(chunk))).ToList();

    public static string Utf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
