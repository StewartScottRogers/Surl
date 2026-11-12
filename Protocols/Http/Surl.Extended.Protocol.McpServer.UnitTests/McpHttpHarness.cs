using System.Buffers.Text;
using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// An MCP server on the Streamable HTTP transport over an in-memory content store, whose clock stands still until
/// a test moves it, and the means to send it one request per <see cref="InMemoryConnection" /> and read the answer.
/// Sessions are the server's, so a session opened on one connection is used on the next.
/// </summary>
internal sealed class McpHttpHarness
{
    public const string NowHttpDate = "Thu, 05 Nov 2026 12:00:00 GMT";

    public const string Host = "127.0.0.1:8080";

    public const string BearerField = "Authorization: Bearer " + UnitTestBearerAuthenticationPolicy.Token;

    public const string AcceptField = "Accept: application/json, text/event-stream";

    public const string JsonField = "Content-Type: application/json";

    public const string InitializeMessage =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"ExampleClient","version":"1.0.0"}}}""";

    public static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    public McpHttpHarness(bool allowsAnonymous = false, string? listenPath = null, ContentExposureOptions? exposure = null)
    {
        TimeProvider = new FixedTimeProvider(Now);
        FileSystem = new InMemoryContentFileSystem(TimeProvider);
        Store = new ContentStore(InMemoryContentFileSystem.RootPath, FileSystem, exposure ?? new ContentExposureOptions(), [], []);
        Policy = new UnitTestBearerAuthenticationPolicy(allowsAnonymous);
        Server = new McpProtocolServer(Store, Policy, "1.2.3", TimeProvider, IdSource, listenPath);
    }

    public FixedTimeProvider TimeProvider { get; }

    /// <summary>The clock the exchange runs on when a test needs timers it can move; else <see cref="TimeProvider"/>.</summary>
    public TimeProvider? ExchangeClock { get; set; }

    /// <summary>The connection the latest <see cref="SendRawAsync"/> was served on.</summary>
    public InMemoryConnection? LastConnection { get; private set; }

    /// <summary>The exchange's idle clock.</summary>
    public IIdleClock IdleClock { get; set; } = NoIdleClock.Instance;

    public InMemoryContentFileSystem FileSystem { get; }

    public ContentStore Store { get; }

    public UnitTestBearerAuthenticationPolicy Policy { get; }

    public CountingRandomNumberGenerator IdSource { get; } = new();

    public McpProtocolServer Server { get; }

    public ExchangeLimits Limits { get; set; } = ExchangeLimits.Default;

    /// <summary>The fault plan every exchange asks; none unless a test sets one.</summary>
    public IFaultPlan Faults { get; set; } = new ScriptedFaultPlan();

    public RecordingSurlEventPublisher Events { get; } = new();

    public RecordingExchangeLog Log { get; } = new();

    /// <summary>The session ID the n-th session is given: the n-th call's 32 bytes, base64url without padding.</summary>
    public static string SessionIdNumber(int n) => Base64Url.EncodeToString(CountingRandomNumberGenerator.BytesOfCall(n, 32));

    /// <summary>Sends a POST to <c>/mcp</c> with the Bearer token, both Accept types and JSON, and the message.</summary>
    public Task<string> PostAsync(string message, params string[] fields) =>
        SendAsync("POST", "/mcp", message, [BearerField, AcceptField, JsonField, .. fields]);

    /// <summary>Opens a session and returns its ID.</summary>
    public async Task<string> InitializeAsync(string login = BearerField)
    {
        string response = await SendAsync("POST", "/mcp", InitializeMessage, [login, AcceptField, JsonField]);
        return FieldOf(response, "Mcp-Session-Id")!;
    }

    /// <summary>Sends one request with <c>Host</c>, the fields, a <c>Content-Length</c> when a body is given, and <c>Connection: close</c>.</summary>
    public Task<string> SendAsync(string method, string target, string? body, params string[] fields)
    {
        string length = body is null ? string.Empty : $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n";
        return SendRawAsync(
            $"{method} {target} HTTP/1.1\r\nHost: {Host}\r\n{string.Concat(fields.Select(field => field + "\r\n"))}{length}Connection: close\r\n\r\n{body}");
    }

    public async Task<string> SendRawAsync(string request, TlsSession? tlsSession = null)
    {
        InMemoryConnection connection = new([Encoding.UTF8.GetBytes(request)], initialTlsSession: tlsSession);
        LastConnection = connection;
        await Server.ServeAsync(connection, Context());
        return Encoding.UTF8.GetString(connection.WrittenBytes);
    }

    public ExchangeContext Context() =>
        new(
            1,
            new ListenUrl("mcp", "127.0.0.1", 8080).WithBoundPort(8080),
            new IPEndPoint(IPAddress.Loopback, 8080),
            new IPEndPoint(IPAddress.Loopback, 50000),
            Log,
            ExchangeClock ?? TimeProvider,
            CancellationToken.None)
        {
            IdleClock = IdleClock,
            Limits = Limits,
            Faults = Faults,
            Events = Events,
        };

    public static string StatusLineOf(string response) => response[..response.IndexOf("\r\n", StringComparison.Ordinal)];

    public static string BodyOf(string response) => response[(response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];

    public static string? FieldOf(string response, string name)
    {
        string head = response[..response.IndexOf("\r\n\r\n", StringComparison.Ordinal)];
        return head.Split("\r\n").Skip(1)
            .Where(line => line.StartsWith(name + ": ", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[(name.Length + 2)..])
            .FirstOrDefault();
    }

    /// <summary>An answer with no body: the status line, Date, Server, the extra fields, Content-Length: 0 and Connection: close.</summary>
    public static string EmptyAnswer(string status, string extraFields = "") =>
        $"HTTP/1.1 {status}\r\nDate: {NowHttpDate}\r\nServer: surl\r\n{extraFields}Content-Length: 0\r\nConnection: close\r\n\r\n";

    /// <summary>
    /// A <c>tools/call</c>'s <c>200</c>: Date, Server, <c>Content-Type: text/event-stream</c>, <c>Transfer-Encoding:
    /// chunked</c> and Connection: close, then the event as one chunk and the last chunk (RFC 9112 section 7.1).
    /// </summary>
    public static string EventStreamAnswer(string streamedEvent) =>
        $"HTTP/1.1 200 OK\r\nDate: {NowHttpDate}\r\nServer: surl\r\nContent-Type: text/event-stream\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n"
        + $"{Encoding.UTF8.GetByteCount(streamedEvent):x}\r\n{streamedEvent}\r\n0\r\n\r\n";

    /// <summary>An answer with a body: the status line, Date, Server, Content-Type, the extra fields, Content-Length and Connection: close.</summary>
    public static string BodyAnswer(string status, string mediaType, string body, string extraFields = "") =>
        $"HTTP/1.1 {status}\r\nDate: {NowHttpDate}\r\nServer: surl\r\nContent-Type: {mediaType}\r\n{extraFields}"
        + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
}
