using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Surl.Content;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// An Azure Storage server on an in-memory content store whose clock stands still, and the means
/// to send it requests through <see cref="InMemoryConnection"/> and read what it answers.
/// </summary>
internal sealed partial class AzureServerHarness
{
    /// <summary>
    /// The clock: 2026-11-05 12:00:00 UTC. Every file the harness writes is last written then.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    public const string NowHttpDate = "Thu, 05 Nov 2026 12:00:00 GMT";

    public const string NowErrorTime = "2026-11-05T12:00:00.0000000Z";

    /// <summary>
    /// The fields a bearer request carries: the version and the token the policy accepts.
    /// </summary>
    public static readonly string[] Bearer = ["x-ms-version: 2025-01-05", $"Authorization: Bearer {UnitTestBearerAuthenticationPolicy.Token}"];

    public AzureServerHarness(bool allowsAnonymous = false, long maxTotalBytes = InMemoryContentFileSystem.DefaultMaxTotalBytes, bool persistsIndex = false, IReadOnlyList<ContentShare>? shares = null, string accountName = "", ContentExposureOptions? exposure = null, IReadOnlyList<ContentBucket>? buckets = null, int maxIndexEntries = ObjectMetadataIndex.DefaultMaxEntries, IFaultPlan? storeFaults = null, IIdentifierRandom? serverIdentifiers = null)
    {
        TimeProvider = new FixedTimeProvider(Now);
        FileSystem = new InMemoryContentFileSystem(TimeProvider, maxTotalBytes);
        Store = new ContentStore(InMemoryContentFileSystem.RootPath, FileSystem, exposure ?? new ContentExposureOptions(), shares ?? [], buckets ?? [], faults: storeFaults);
        Index = new ObjectMetadataIndex(FileSystem, InMemoryContentFileSystem.RootPath, persistsIndex, maxIndexEntries);
        Policy = new UnitTestBearerAuthenticationPolicy(allowsAnonymous) { AccountName = accountName };
        Server = new AzureStorageProtocolServer(Store, Index, Policy, TimeProvider, serverIdentifiers);
    }

    public FixedTimeProvider TimeProvider { get; }

    public InMemoryContentFileSystem FileSystem { get; }

    public ContentStore Store { get; }

    public ObjectMetadataIndex Index { get; }

    public UnitTestBearerAuthenticationPolicy Policy { get; }

    public AzureStorageProtocolServer Server { get; }

    public RecordingExchangeLog Log { get; } = new();

    /// <summary>
    /// The fault plan each exchange asks: none unless a test sets one.
    /// </summary>
    public IFaultPlan Faults { get; set; } = EmptyFaultPlan.Instance;

    /// <summary>
    /// The seed every exchange draws its identifiers from; null draws from the cryptographic random number generator.
    /// </summary>
    public long? Seed { get; set; }

    /// <summary>
    /// The event stream each exchange publishes on.
    /// </summary>
    public RecordingSurlEventPublisher Events { get; } = new();

    /// <summary>
    /// The listen scheme each request is served on, which decides the endpoint: <c>az</c> unless a test sets it.
    /// </summary>
    public string Scheme { get; set; } = AzureStorageProtocolServer.BlobScheme;

    /// <summary>
    /// Serves every later request on <c>abfs</c>, the Data Lake endpoint.
    /// </summary>
    public AzureServerHarness ServingDataLake()
    {
        Scheme = AzureStorageProtocolServer.DataLakeScheme;
        return this;
    }

    public static string Root => InMemoryContentFileSystem.RootPath;

    public void CreateFolder(string relativePath) => FileSystem.CreateDirectory(Path.Join(Root, relativePath));

    public void WriteFile(string relativePath, string content) => WriteFile(FileSystem, relativePath, content);

    public static void WriteFile(IContentFileSystem fileSystem, string relativePath, string content)
    {
        using var stream = fileSystem.CreateFileForAsyncWrite(Path.Join(Root, relativePath));
        stream.Write(Encoding.UTF8.GetBytes(content));
    }

    public ExchangeContext Context(ExchangeLimits? limits = null) => new(
        1,
        new ListenUrl(Scheme, "127.0.0.1", 10000).WithBoundPort(10000),
        new IPEndPoint(IPAddress.Loopback, 10000),
        new IPEndPoint(IPAddress.Loopback, 50000),
        Log,
        TimeProvider,
        CancellationToken.None)
    {
        Limits = limits ?? ExchangeLimits.Default,
        Faults = Faults,
        Events = Events,
        Identifiers = Seed is { } seed ? new SeededIdentifierRandom(seed).ForExchange(1) : CryptographicIdentifierRandom.Instance,
    };

    /// <summary>
    /// Sends one request head with the bearer fields, <c>Host</c> and <c>Connection: close</c>.
    /// </summary>
    public Task<string> SendWithBearerAsync(string method, string target, params string[] fields) =>
        SendAsync(method, target, [.. Bearer, .. fields]);

    /// <summary>
    /// Sends one request head, <c>Host: account.blob.core.windows.net</c> and <c>Connection: close</c>
    /// added, and returns what the server wrote.
    /// </summary>
    public Task<string> SendAsync(string method, string target, params string[] fields) =>
        SendRawAsync($"{method} {target} HTTP/1.1\r\nHost: account.blob.core.windows.net\r\n{string.Concat(fields.Select(field => field + "\r\n"))}Connection: close\r\n\r\n");

    /// <summary>
    /// Sends one request with the bearer fields and a body announced by <c>Content-Length</c>,
    /// <c>Host</c> and <c>Connection: close</c> added, and returns what the server wrote.
    /// </summary>
    public Task<string> SendWithBearerAndBodyAsync(string method, string target, string body, params string[] fields) =>
        SendRawAsync(
            $"{method} {target} HTTP/1.1\r\nHost: account.blob.core.windows.net\r\n{string.Concat(((string[])[.. Bearer, .. fields]).Select(field => field + "\r\n"))}"
                + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");

    /// <summary>
    /// The exposure options a harness that takes writes and listings is made with.
    /// </summary>
    public static ContentExposureOptions Writable { get; } = new() { AllowUploads = true, ListDirectories = true };

    public string ReadFile(string relativePath)
    {
        using var stream = FileSystem.OpenFileForAsyncRead(Path.Join(Root, relativePath));
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    public async Task<string> SendRawAsync(string request, ExchangeLimits? limits = null)
    {
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(request)]);
        await Server.ServeAsync(connection, Context(limits));

        return Encoding.UTF8.GetString(connection.WrittenBytes);
    }

    /// <summary>
    /// The request ID an answer carries, checked to be a lower-case GUID.
    /// </summary>
    public static string RequestIdOf(string response)
    {
        var match = RequestIdField().Match(response);
        Assert.IsTrue(match.Success, response);

        return match.Groups[1].Value;
    }

    /// <summary>
    /// The common fields an answer starts with, after its status line.
    /// </summary>
    public static string CommonFields(string response) =>
        $"Date: {NowHttpDate}\r\nServer: surl\r\nx-ms-request-id: {RequestIdOf(response)}\r\nx-ms-version: 2025-01-05\r\n";

    /// <summary>
    /// The answer an error is expected to be: the status, the common fields, <c>x-ms-error-code</c>,
    /// <paramref name="extraFields"/>, and the <c>Error</c> body with the code, the message and its
    /// <c>RequestId:</c> and <c>Time:</c> lines, then <paramref name="elements"/>.
    /// </summary>
    public static string ExpectedError(string response, string status, string code, string message, string elements = "", string extraFields = "")
    {
        var requestId = RequestIdOf(response);
        var body = $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Error><Code>{code}</Code><Message>{message}\nRequestId:{requestId}\nTime:{NowErrorTime}</Message>{elements}</Error>";

        return $"HTTP/1.1 {status}\r\n{CommonFields(response)}x-ms-error-code: {code}\r\n{extraFields}"
            + $"Content-Type: application/xml\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
    }

    /// <summary>
    /// The answer a Data Lake error is expected to be: the status, the common fields,
    /// <c>x-ms-error-code</c>, <paramref name="extraFields"/>, and the JSON error body with the code and
    /// the message and its <c>RequestId:</c> and <c>Time:</c> lines ("Path - Create", <c>DataLakeStorageError</c>).
    /// </summary>
    public static string ExpectedJsonError(string response, string status, string code, string message, string extraFields = "")
    {
        var requestId = RequestIdOf(response);
        var body = $"{{\"error\":{{\"code\":\"{code}\",\"message\":\"{message}\\nRequestId:{requestId}\\nTime:{NowErrorTime}\"}}}}";

        return $"HTTP/1.1 {status}\r\n{CommonFields(response)}x-ms-error-code: {code}\r\n{extraFields}"
            + $"Content-Type: application/json;charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
    }

    [GeneratedRegex("\r\nx-ms-request-id: ([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\r\n")]
    private static partial Regex RequestIdField();
}
