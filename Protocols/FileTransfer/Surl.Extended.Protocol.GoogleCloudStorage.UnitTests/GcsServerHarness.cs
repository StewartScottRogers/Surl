using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// A Cloud Storage server on an in-memory content store whose clock stands still, and the means to
/// send it requests through <see cref="InMemoryConnection"/> and read what it answers.
/// </summary>
internal sealed class GcsServerHarness
{
    /// <summary>
    /// The clock: 2026-11-05 12:00:00 UTC. Every file the harness writes is last written then.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    public const string NowHttpDate = "Thu, 05 Nov 2026 12:00:00 GMT";

    public const string NowListTime = "2026-11-05T12:00:00.000Z";

    /// <summary>
    /// <see cref="Now"/> as a generation: microseconds since the Unix epoch.
    /// </summary>
    public const string NowGeneration = "1793880000000000";

    public const string BearerField = $"Authorization: Bearer {UnitTestGcsAuthenticationPolicy.Token}";

    public GcsServerHarness(bool allowsAnonymous = false, long maxTotalBytes = InMemoryContentFileSystem.DefaultMaxTotalBytes, bool persistsIndex = false, string accountName = "", ContentExposureOptions? exposure = null, IReadOnlyList<ContentShare>? shares = null, IReadOnlyList<ContentBucket>? buckets = null, int maxIndexEntries = ObjectMetadataIndex.DefaultMaxEntries, string listenHost = "127.0.0.1")
    {
        TimeProvider = new FixedTimeProvider(Now);
        FileSystem = new InMemoryContentFileSystem(TimeProvider, maxTotalBytes);
        Store = new ContentStore(InMemoryContentFileSystem.RootPath, FileSystem, exposure ?? new ContentExposureOptions(), shares ?? [], buckets ?? []);
        Index = new ObjectMetadataIndex(FileSystem, InMemoryContentFileSystem.RootPath, persistsIndex, maxIndexEntries);
        Policy = new UnitTestGcsAuthenticationPolicy(allowsAnonymous) { AccountName = accountName };
        Server = new GoogleCloudStorageProtocolServer(Store, Index, Policy, TimeProvider);
        ListenHost = listenHost;
    }

    /// <summary>
    /// The exposure options a harness that takes writes and listings is made with.
    /// </summary>
    public static ContentExposureOptions Writable { get; } = new() { AllowUploads = true, ListDirectories = true };

    public static string Root => InMemoryContentFileSystem.RootPath;

    public FixedTimeProvider TimeProvider { get; }

    public InMemoryContentFileSystem FileSystem { get; }

    public ContentStore Store { get; }

    public ObjectMetadataIndex Index { get; }

    public UnitTestGcsAuthenticationPolicy Policy { get; }

    public GoogleCloudStorageProtocolServer Server { get; }

    public RecordingExchangeLog Log { get; } = new();

    public string ListenHost { get; }

    /// <summary>
    /// The fields every answer starts with, after its status line.
    /// </summary>
    public static string CommonFields => $"Date: {NowHttpDate}\r\nServer: surl\r\n";

    /// <summary>
    /// The answer an error is expected to be: the status, the common fields, <paramref name="extraFields"/>,
    /// and the one-line <c>Error</c> body.
    /// </summary>
    public static string ExpectedError(string status, string code, string message, string extraFields = "")
    {
        var body = $"<?xml version='1.0' encoding='UTF-8'?><Error><Code>{code}</Code><Message>{message}</Message></Error>";

        return $"HTTP/1.1 {status}\r\n{CommonFields}{extraFields}Content-Type: application/xml; charset=UTF-8\r\n"
            + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
    }

    public void CreateFolder(string relativePath) => FileSystem.CreateDirectory(Path.Join(Root, relativePath));

    public void WriteFile(string relativePath, string content) => WriteFile(FileSystem, relativePath, content);

    public static void WriteFile(IContentFileSystem fileSystem, string relativePath, string content)
    {
        using var stream = fileSystem.CreateFileForAsyncWrite(Path.Join(Root, relativePath));
        stream.Write(Encoding.UTF8.GetBytes(content));
    }

    public string ReadFile(string relativePath)
    {
        using var stream = FileSystem.OpenFileForAsyncRead(Path.Join(Root, relativePath));
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    public ContentEntryKind EntryKind(string relativePath) => FileSystem.GetEntryKind(Path.Join(Root, relativePath));

    public ExchangeContext Context(ExchangeLimits? limits = null) => new(
        1,
        new ListenUrl(GoogleCloudStorageProtocolServer.Scheme, ListenHost, 4443).WithBoundPort(4443),
        new IPEndPoint(IPAddress.Loopback, 4443),
        new IPEndPoint(IPAddress.Loopback, 50000),
        Log,
        TimeProvider,
        CancellationToken.None)
    {
        Limits = limits ?? ExchangeLimits.Default,
    };

    /// <summary>
    /// Sends one request head with the Bearer token, <c>Host</c> and <c>Connection: close</c>.
    /// </summary>
    public Task<string> SendWithBearerAsync(string method, string target, params string[] fields) =>
        SendAsync(method, target, [BearerField, .. fields]);

    /// <summary>
    /// Sends one request head, <c>Host: 127.0.0.1:4443</c> and <c>Connection: close</c> added, and
    /// returns what the server wrote.
    /// </summary>
    public Task<string> SendAsync(string method, string target, params string[] fields) =>
        SendRawAsync($"{method} {target} HTTP/1.1\r\nHost: 127.0.0.1:4443\r\n{Lines(fields)}Connection: close\r\n\r\n");

    /// <summary>
    /// Sends one request with the Bearer token and a body announced by <c>Content-Length</c>.
    /// </summary>
    public Task<string> SendWithBearerAndBodyAsync(string method, string target, string body, params string[] fields) =>
        SendRawAsync(
            $"{method} {target} HTTP/1.1\r\nHost: 127.0.0.1:4443\r\n{Lines([BearerField, .. fields])}"
                + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");

    public async Task<string> SendRawAsync(string request, ExchangeLimits? limits = null)
    {
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(request)]);
        await Server.ServeAsync(connection, Context(limits));

        return Encoding.UTF8.GetString(connection.WrittenBytes);
    }

    private static string Lines(IEnumerable<string> fields) => string.Concat(fields.Select(field => field + "\r\n"));
}
