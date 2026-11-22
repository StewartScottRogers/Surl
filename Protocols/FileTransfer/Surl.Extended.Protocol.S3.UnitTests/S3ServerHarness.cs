using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Surl.Content;
using Surl.Core;
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

    public S3ServerHarness(long maxTotalBytes = InMemoryContentFileSystem.DefaultMaxTotalBytes, bool persistsIndex = false, IReadOnlyList<ContentShare>? shares = null, IReadOnlyList<ContentBucket>? buckets = null, ContentExposureOptions? exposure = null, string listenHost = "127.0.0.1", int maxIndexEntries = ObjectMetadataIndex.DefaultMaxEntries, UnitTestS3AuthenticationPolicy? policy = null, IIdentifierRandom? serverIdentifiers = null, ScriptedFaultPlan? faults = null)
    {
        TimeProvider = new FixedTimeProvider(Now);
        FileSystem = new InMemoryContentFileSystem(TimeProvider, maxTotalBytes);
        Faults = faults ?? new ScriptedFaultPlan();
        Store = new ContentStore(InMemoryContentFileSystem.RootPath, FileSystem, exposure ?? new ContentExposureOptions(), shares ?? [], buckets ?? [], Events, Faults);
        ListenHost = listenHost;
        Index = new ObjectMetadataIndex(FileSystem, InMemoryContentFileSystem.RootPath, persistsIndex, maxIndexEntries);
        Policy = policy ?? new UnitTestS3AuthenticationPolicy();
        Server = new S3ProtocolServer(Store, Index, Policy, TimeProvider, serverIdentifiers);
    }

    /// <summary>
    /// Uploads and listings allowed, as --allow-uploads and --list-directories set them.
    /// </summary>
    public static ContentExposureOptions Writable { get; } = new() { AllowUploads = true, ListDirectories = true };

    public string ListenHost { get; }

    public FixedTimeProvider TimeProvider { get; }

    public InMemoryContentFileSystem FileSystem { get; }

    public ContentStore Store { get; }

    public ObjectMetadataIndex Index { get; }

    /// <summary>
    /// The policy judging every request; by default an unsigned request logs in as
    /// <see cref="UnitTestS3AuthenticationPolicy.ExampleAccessKeyId"/>.
    /// </summary>
    public UnitTestS3AuthenticationPolicy Policy { get; }

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

    /// <summary>
    /// True keeps the scripted client open after its request, so a delay hold is never ended by the client seeming to leave;
    /// the test then calls <see cref="WaitForCompleteResponseAsync"/> and <see cref="StopServing"/> before awaiting the exchange.
    /// </summary>
    public bool ClientStaysOpen { get; set; }

    private CancellationTokenSource? servingCancellation;

    /// <summary>
    /// Waits, off the fake clock, until the server has written a whole response to the open client.
    /// </summary>
    public async Task WaitForCompleteResponseAsync()
    {
        using var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!ResponseIsComplete())
        {
            await Task.Delay(1, giveUp.Token);
        }
    }

    /// <summary>
    /// Ends an exchange whose client stayed open, as the listener does when it stops.
    /// </summary>
    public void StopServing() => servingCancellation?.Cancel();

    private bool ResponseIsComplete()
    {
        var written = LastWrittenText();
        var headEnd = written.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (headEnd < 0)
        {
            return false;
        }

        var lengthField = Regex.Match(written[..headEnd], @"\r\nContent-Length: (\d+)", RegexOptions.IgnoreCase);

        return !lengthField.Success || Encoding.UTF8.GetByteCount(written[(headEnd + 4)..]) >= int.Parse(lengthField.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private InMemoryConnection? lastConnection;

    private string LastWrittenText() => Encoding.UTF8.GetString(lastConnection!.WrittenBytes);

    private async Task ServeUntilDoneAsync(InMemoryConnection connection, Func<ExchangeContext> context)
    {
        servingCancellation = ClientStaysOpen ? new CancellationTokenSource() : null;
        try
        {
            await Server.ServeAsync(connection, context());
        }
        catch (OperationCanceledException) when (servingCancellation is { IsCancellationRequested: true })
        {
        }
    }

    public ExchangeContext Context(ExchangeLimits? limits = null) => new(
        1,
        new ListenUrl(S3ProtocolServer.Scheme, ListenHost, 9000).WithBoundPort(9000),
        new IPEndPoint(IPAddress.Loopback, 9000),
        new IPEndPoint(IPAddress.Loopback, 50000),
        Log,
        TimeProvider,
        servingCancellation?.Token ?? CancellationToken.None)
    {
        Limits = limits ?? ExchangeLimits.Default,
        Faults = Faults,
        Events = Events,
        IdleClock = IdleClock,
        Identifiers = Seed is { } seed ? new SeededIdentifierRandom(seed).ForExchange(1) : CryptographicIdentifierRandom.Instance,
    };

    /// <summary>
    /// The fault plan every exchange asks; by default one with no rule.
    /// </summary>
    public ScriptedFaultPlan Faults { get; set; }

    /// <summary>
    /// The idle clock every exchange pauses while a fault rule holds a request.
    /// </summary>
    public RecordingIdleClock IdleClock { get; } = new();

    /// <summary>
    /// Sends one request on a connection the test can inspect afterwards.
    /// </summary>
    public async Task<InMemoryConnection> ServeRawAsync(string request)
    {
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(request)]);
        await Server.ServeAsync(connection, Context());

        return connection;
    }

    /// <summary>
    /// The seed every exchange draws its identifiers from; null draws from the cryptographic random number generator.
    /// </summary>
    public long? Seed { get; set; }

    /// <summary>
    /// The event stream every exchange publishes to.
    /// </summary>
    public RecordingSurlEventPublisher Events { get; } = new();

    /// <summary>
    /// Sends one request head, <c>Host: s3.test</c> and <c>Connection: close</c> added, and returns
    /// what the server wrote.
    /// </summary>
    public Task<string> SendAsync(string method, string target, params string[] fields) =>
        SendRawAsync($"{method} {target} HTTP/1.1\r\nHost: s3.test\r\n{string.Concat(fields.Select(field => field + "\r\n"))}Connection: close\r\n\r\n");

    /// <summary>
    /// Sends one request with a body announced by Content-Length, Host: s3.test and
    /// Connection: close added, and returns what the server wrote.
    /// </summary>
    public Task<string> SendWithBodyAsync(string method, string target, string body, params string[] fields) =>
        SendRawAsync($"{method} {target} HTTP/1.1\r\nHost: s3.test\r\n{string.Concat(fields.Select(field => field + "\r\n"))}Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n" + body);

    public string ReadFile(string relativePath)
    {
        using var stream = FileSystem.OpenFileForAsyncRead(Path.Join(Root, relativePath));
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    public async Task<string> SendRawAsync(string request, ExchangeLimits? limits = null)
    {
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(request)], peerHalfClosesWhenExhausted: !ClientStaysOpen);
        lastConnection = connection;
        await ServeUntilDoneAsync(connection, () => Context(limits));

        return Encoding.UTF8.GetString(connection.WrittenBytes);
    }

    /// <summary>
    /// Sends one request on a connection that is TLS from the start, and returns what the server wrote.
    /// </summary>
    public async Task<string> SendRawOverTlsAsync(string request)
    {
        var tls = new TlsSession(System.Security.Authentication.SslProtocols.Tls13, System.Net.Security.TlsCipherSuite.TLS_AES_128_GCM_SHA256, null, null, null);
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(request)], initialTlsSession: tls);
        await Server.ServeAsync(connection, Context());

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
