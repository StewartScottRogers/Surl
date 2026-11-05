using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// An S3 server on an in-memory content store whose clock stands still, and the means to send
/// it requests through <see cref="InMemoryConnection"/> and read what it answers.
/// </summary>
internal sealed partial class S3ServerHarness
{
    /// <summary>
    /// The clock: 2026-11-05 12:00:00 UTC, written by IMF-fixdate as <see cref="NowHttpDate"/>.
    /// Every file the harness writes is last written then.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    public const string NowHttpDate = "Thu, 05 Nov 2026 12:00:00 GMT";

    public S3ServerHarness(long maxTotalBytes = InMemoryContentFileSystem.DefaultMaxTotalBytes, bool persistsIndex = false, IReadOnlyList<ContentShare>? shares = null, IReadOnlyList<ContentBucket>? buckets = null)
    {
        TimeProvider = new FixedTimeProvider(Now);
        FileSystem = new InMemoryContentFileSystem(TimeProvider, maxTotalBytes);
        Store = new ContentStore(InMemoryContentFileSystem.RootPath, FileSystem, new ContentExposureOptions(), shares ?? [], buckets ?? []);
        Index = new ObjectMetadataIndex(FileSystem, InMemoryContentFileSystem.RootPath, persistsIndex);
        Server = new S3ProtocolServer(Store, Index, TimeProvider);
    }

    public FixedTimeProvider TimeProvider { get; }

    public InMemoryContentFileSystem FileSystem { get; }

    public ContentStore Store { get; }

    public ObjectMetadataIndex Index { get; }

    public S3ProtocolServer Server { get; }

    public RecordingExchangeLog Log { get; } = new();

    public static string Root => InMemoryContentFileSystem.RootPath;

    public void CreateFolder(string relativePath) => CreateFolder(FileSystem, relativePath);

    public static void CreateFolder(IContentFileSystem fileSystem, string relativePath) =>
        fileSystem.CreateDirectory(Path.Join(Root, relativePath));

    public void WriteFile(string relativePath, string content) => WriteFile(FileSystem, relativePath, content);

    public static void WriteFile(IContentFileSystem fileSystem, string relativePath, string content)
    {
        using var stream = fileSystem.CreateFileForAsyncWrite(Path.Join(Root, relativePath));
        stream.Write(Encoding.UTF8.GetBytes(content));
    }

    public ExchangeContext Context(ExchangeLimits? limits = null) => new(
        1,
        new ListenUrl(S3ProtocolServer.Scheme, "127.0.0.1", 9000).WithBoundPort(9000),
        new IPEndPoint(IPAddress.Loopback, 9000),
        new IPEndPoint(IPAddress.Loopback, 50000),
        Log,
        TimeProvider,
        CancellationToken.None)
    {
        Limits = limits ?? ExchangeLimits.Default,
    };

    /// <summary>
    /// Sends one request head, <c>Host: s3.test</c> and <c>Connection: close</c> added, and returns
    /// what the server wrote.
    /// </summary>
    public Task<string> SendAsync(string method, string target, params string[] fields) =>
        SendRawAsync($"{method} {target} HTTP/1.1\r\nHost: s3.test\r\n{string.Concat(fields.Select(field => field + "\r\n"))}Connection: close\r\n\r\n");

    public async Task<string> SendRawAsync(string request, ExchangeLimits? limits = null)
    {
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(request)]);
        await Server.ServeAsync(connection, Context(limits));

        return Encoding.UTF8.GetString(connection.WrittenBytes);
    }

    /// <summary>
    /// The request ID an answer carries, checked to be 16 upper-case hexadecimal digits.
    /// </summary>
    public static string RequestIdOf(string response)
    {
        var match = RequestIdField().Match(response);
        Assert.IsTrue(match.Success, response);

        return match.Groups[1].Value;
    }

    /// <summary>
    /// The answer an error is expected to be, with the request ID the response carries.
    /// </summary>
    public static string ExpectedError(string response, string status, string body)
    {
        var requestId = RequestIdOf(response);
        var fullBody = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Error>{body.Replace("{RequestId}", requestId, StringComparison.Ordinal)}</Error>";

        return $"HTTP/1.1 {status}\r\nDate: {NowHttpDate}\r\nServer: surl\r\nx-amz-request-id: {requestId}\r\n"
            + $"Content-Type: application/xml\r\nContent-Length: {Encoding.UTF8.GetByteCount(fullBody)}\r\nConnection: close\r\n\r\n{fullBody}";
    }

    [GeneratedRegex("\r\nx-amz-request-id: ([0-9A-F]{16})\r\n")]
    private static partial Regex RequestIdField();
}
