using System.Net;
using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// An OCI registry on an in-memory content store whose clock stands still, the means to lay out
/// repositories in it as OCI Image Layouts, and the means to send it requests through
/// <see cref="InMemoryConnection"/> and read what it answers.
/// </summary>
internal sealed class OciRegistryServerHarness
{
    /// <summary>
    /// The clock: 2026-11-05 12:00:00 UTC, written by IMF-fixdate as <see cref="NowHttpDate"/>.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    public const string NowHttpDate = "Thu, 05 Nov 2026 12:00:00 GMT";

    public const string ManifestMediaType = "application/vnd.oci.image.manifest.v1+json";

    public const string IndexMediaType = "application/vnd.oci.image.index.v1+json";

    /// <summary>
    /// The head every answer starts with after its status line.
    /// </summary>
    public const string CommonFields = $"Date: {NowHttpDate}\r\nServer: surl\r\nDocker-Distribution-API-Version: registry/2.0\r\n";

    private int sessionCount;

    public OciRegistryServerHarness()
        : this(new ContentExposureOptions())
    {
    }

    /// <summary>
    /// A registry whose store exposes what <paramref name="options"/> say, with
    /// <paramref name="shares"/>; each share's folder is created under <c>/shares</c> of the root
    /// file system. Upload session UUIDs are <see cref="SessionId(int)"/> 1, 2, … in order.
    /// </summary>
    public OciRegistryServerHarness(ContentExposureOptions options, params Func<InMemoryContentFileSystem, ContentShare>[] shares)
        : this(OciTokenBook.MaxLiveTokens, options, shares)
    {
    }

    private OciRegistryServerHarness(int maxLiveTokens, ContentExposureOptions options, Func<InMemoryContentFileSystem, ContentShare>[] shares, bool keepsMetadataIndex = true)
    {
        TimeProvider = new FixedTimeProvider(Now);
        FileSystem = new InMemoryContentFileSystem(TimeProvider);
        CountingFileSystem = new UnitTestCountingContentFileSystem(FileSystem);
        Store = new ContentStore(InMemoryContentFileSystem.RootPath, CountingFileSystem, options, [.. shares.Select(share => share(FileSystem))], []);
        MetadataIndex = keepsMetadataIndex ? new ObjectMetadataIndex(CountingFileSystem, InMemoryContentFileSystem.RootPath, persists: false) : null;
        Server = new OciRegistryProtocolServer(Store, Policy, TimeProvider, () => Guid.Parse(SessionId(Interlocked.Increment(ref sessionCount))), maxLiveTokens, MetadataIndex);
    }

    /// <summary>
    /// A registry with no object metadata index, so it hashes every file on every read.
    /// </summary>
    public static OciRegistryServerHarness WithoutMetadataIndex() => new(OciTokenBook.MaxLiveTokens, new ContentExposureOptions(), [], keepsMetadataIndex: false);

    /// <summary>
    /// The root file system as the store sees it, counting each file's reads.
    /// </summary>
    public UnitTestCountingContentFileSystem CountingFileSystem { get; }

    /// <summary>
    /// The object metadata index the server caches verified digests in; <see langword="null"/>
    /// for <see cref="WithoutMetadataIndex"/>.
    /// </summary>
    public ObjectMetadataIndex? MetadataIndex { get; }

    /// <summary>
    /// How many times the file at <paramref name="relativePath"/> of the root was opened for reading.
    /// </summary>
    public int ReadsOf(string relativePath) => CountingFileSystem.ReadsOf(Path.Join([InMemoryContentFileSystem.RootPath, .. relativePath.Split('/')]));

    /// <summary>
    /// Replaces the file at <paramref name="relativePath"/> through the content store, as another
    /// protocol's upload would.
    /// </summary>
    public async Task ReplaceThroughStoreAsync(string relativePath, byte[] content)
    {
        var store = new ContentStore(InMemoryContentFileSystem.RootPath, CountingFileSystem, new ContentExposureOptions { AllowUploads = true });
        using var source = new MemoryStream(content);

        Assert.AreEqual(ContentUploadResult.Written, await store.WriteUploadAsync(store.MapRequestPath("/" + relativePath), source, CancellationToken.None));
    }

    /// <summary>
    /// A registry that accepts uploads and keeps at most <paramref name="maxLiveTokens"/> live tokens.
    /// </summary>
    public static OciRegistryServerHarness WithTokenBound(int maxLiveTokens) =>
        new(maxLiveTokens, new ContentExposureOptions { AllowUploads = true }, []);

    /// <summary>
    /// The policy the token endpoint checks logins with: the account
    /// <see cref="UnitTestAuthenticationPolicy.UserName"/>, anonymous logins refused.
    /// </summary>
    public UnitTestAuthenticationPolicy Policy { get; } = new();

    /// <summary>
    /// The TLS session of the connection each request is sent on; <see langword="null"/> is plain text.
    /// </summary>
    public TlsSession? TlsSession { get; set; }

    /// <summary>
    /// Where each exchange's random bytes, and so each token, come from.
    /// </summary>
    public CountingIdentifierRandom Random { get; } = new();

    /// <summary>
    /// Whether a <c>/v2/</c> request sent without <c>Authorization</c> first fetches a token from
    /// <c>/token</c> as <see cref="UnitTestAuthenticationPolicy.UserName"/>, asking every action
    /// in its repository and <c>pull</c> in a mount's <c>from</c>, and carries it; on by default,
    /// so a test of what a request does with a token need not fetch one itself.
    /// </summary>
    public bool SendsTokens { get; set; } = true;

    /// <summary>
    /// A registry that accepts uploads.
    /// </summary>
    public static OciRegistryServerHarness AllowingUploads() => new(new ContentExposureOptions { AllowUploads = true });

    /// <summary>
    /// A registry that accepts uploads, with a read-only share <c>ro</c> already holding the
    /// repository <c>ro/app</c>, so the store refuses every write to it.
    /// </summary>
    public static OciRegistryServerHarness WithReadOnlyRepository()
    {
        var harness = new OciRegistryServerHarness(
            new ContentExposureOptions { AllowUploads = true },
            Share("ro", share => new ContentShare(share.Name, share.Path, share.FileSystem) { IsReadOnly = true }));
        harness.CreateRepository("shares/ro/app");

        return harness;
    }

    /// <summary>
    /// <see cref="ExchangeLimits.Default"/> with the upload limit set to <paramref name="maxUploadBytes"/>.
    /// </summary>
    public static ExchangeLimits LimitsWithMaxUpload(long maxUploadBytes)
    {
        var limits = ExchangeLimits.Default;

        return new ExchangeLimits(limits.HeadTimeout, limits.MaxRequestHeadBytes, limits.MaxLineBytes, limits.MaxMessageBytes, maxUploadBytes);
    }

    /// <summary>
    /// The UUID of the <paramref name="number"/>th upload session opened.
    /// </summary>
    public static string SessionId(int number) => $"00000000-0000-4000-8000-{number:D12}";

    /// <summary>
    /// A share named <paramref name="name"/> whose folder exists, set up by <paramref name="configure"/>.
    /// </summary>
    public static Func<InMemoryContentFileSystem, ContentShare> Share(string name, Func<ContentShare, ContentShare> configure) => fileSystem =>
    {
        var shares = Path.Join(InMemoryContentFileSystem.RootPath, "shares");
        if (fileSystem.GetEntryKind(shares) == ContentEntryKind.None)
        {
            fileSystem.CreateDirectory(shares);
        }

        fileSystem.CreateDirectory(Path.Join(shares, name));

        return configure(new ContentShare(name, Path.Join(shares, name), fileSystem));
    };

    /// <summary>
    /// The bytes of the file at <paramref name="relativePath"/>, or <see langword="null"/> when there is none.
    /// </summary>
    public byte[]? ReadFile(string relativePath)
    {
        var location = Path.Join(InMemoryContentFileSystem.RootPath, relativePath);
        if (FileSystem.GetEntryKind(location) != ContentEntryKind.File)
        {
            return null;
        }

        using var stream = FileSystem.OpenFileForAsyncRead(location);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }

    /// <summary>
    /// Sends one request with <paramref name="body"/> and its <c>Content-Length</c>,
    /// <c>Host: registry.test</c> and <c>Connection: close</c> added.
    /// </summary>
    public Task<string> SendWithBodyAsync(string method, string target, string body, params string[] fields) =>
        SendRawAsync($"{method} {target} HTTP/1.1\r\nHost: registry.test\r\n{string.Concat(fields.Select(field => field + "\r\n"))}Content-Length: {Encoding.Latin1.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");

    public FixedTimeProvider TimeProvider { get; }

    public InMemoryContentFileSystem FileSystem { get; }

    public ContentStore Store { get; }

    public OciRegistryProtocolServer Server { get; }

    public RecordingExchangeLog Log { get; } = new();

    public static string Sha256Of(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static string Sha256Of(string text) => Sha256Of(Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// The error body the specification gives a code and message.
    /// </summary>
    public static string ErrorBody(string code, string message) => $"{{\"errors\":[{{\"code\":\"{code}\",\"message\":\"{message}\"}}]}}";

    /// <summary>
    /// The whole answer to a GET refused with <paramref name="code"/>.
    /// </summary>
    public static string ExpectedError(string status, string code, string message, string extraFields = "")
    {
        var body = ErrorBody(code, message);

        return $"HTTP/1.1 {status}\r\n{CommonFields}Content-Type: application/json\r\n{extraFields}Content-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
    }

    /// <summary>
    /// Makes <paramref name="name"/> a repository: its folders and its <c>oci-layout</c>.
    /// </summary>
    public void CreateRepository(string name) => WriteFile($"{name}/oci-layout", "{\"imageLayoutVersion\":\"1.0.0\"}");

    /// <summary>
    /// Writes <paramref name="content"/> as a blob of <paramref name="name"/> under its sha256
    /// digest, and returns the digest.
    /// </summary>
    public string WriteBlob(string name, string content) => WriteBlob(name, Encoding.UTF8.GetBytes(content));

    public string WriteBlob(string name, byte[] content)
    {
        var digest = Sha256Of(content);
        WriteFile($"{name}/blobs/sha256/{digest["sha256:".Length..]}", content);

        return digest;
    }

    /// <summary>
    /// Writes <c>index.json</c> listing <paramref name="descriptors"/>, each one JSON object.
    /// </summary>
    public void WriteIndex(string name, params string[] descriptors) =>
        WriteFile($"{name}/index.json", $"{{\"schemaVersion\":2,\"mediaType\":\"{IndexMediaType}\",\"manifests\":[{string.Join(',', descriptors)}]}}");

    /// <summary>
    /// A descriptor of <paramref name="content"/>, tagged when <paramref name="tag"/> is given.
    /// </summary>
    public static string Descriptor(string mediaType, string content, string? tag = null, string? artifactType = null)
    {
        var bytes = Encoding.UTF8.GetByteCount(content);
        var artifact = artifactType is null ? string.Empty : $",\"artifactType\":\"{artifactType}\"";
        var annotations = tag is null ? string.Empty : $",\"annotations\":{{\"org.opencontainers.image.ref.name\":\"{tag}\"}}";

        return $"{{\"mediaType\":\"{mediaType}\",\"digest\":\"{Sha256Of(content)}\",\"size\":{bytes}{artifact}{annotations}}}";
    }

    public void WriteFile(string relativePath, string content) => WriteFile(relativePath, Encoding.UTF8.GetBytes(content));

    public void WriteFile(string relativePath, byte[] content)
    {
        var segments = relativePath.Split('/');
        var folder = InMemoryContentFileSystem.RootPath;
        foreach (var segment in segments[..^1])
        {
            folder = Path.Join(folder, segment);
            if (FileSystem.GetEntryKind(folder) == ContentEntryKind.None)
            {
                FileSystem.CreateDirectory(folder);
            }
        }

        using var stream = FileSystem.CreateFileForAsyncWrite(Path.Join(InMemoryContentFileSystem.RootPath, relativePath));
        stream.Write(content);
    }

    /// <summary>
    /// The limits each exchange runs under; a test lowers the upload limit to reach it.
    /// </summary>
    public ExchangeLimits Limits { get; set; } = ExchangeLimits.Default;

    public ExchangeContext Context() => new(
        1,
        new ListenUrl(OciRegistryProtocolServer.Scheme, "127.0.0.1", 5000).WithBoundPort(5000),
        new IPEndPoint(IPAddress.Loopback, 5000),
        new IPEndPoint(IPAddress.Loopback, 50000),
        Log,
        TimeProvider,
        CancellationToken.None)
    {
        Limits = Limits,
        Identifiers = Random,
    };

    /// <summary>
    /// Sends one request head, <c>Host: registry.test</c> and <c>Connection: close</c> added, and
    /// returns what the server wrote.
    /// </summary>
    public Task<string> SendAsync(string method, string target, params string[] fields) =>
        SendRawAsync($"{method} {target} HTTP/1.1\r\nHost: registry.test\r\n{string.Concat(fields.Select(field => field + "\r\n"))}Connection: close\r\n\r\n");

    public async Task<string> SendRawAsync(string request) =>
        await ServeAsync(SendsTokens && request.Contains(" /v2/", StringComparison.Ordinal) && !request.Contains("\r\nAuthorization:", StringComparison.OrdinalIgnoreCase)
            ? await WithTokenAsync(request)
            : request);

    /// <summary>
    /// Fetches a token as <see cref="UnitTestAuthenticationPolicy.UserName"/> for
    /// <paramref name="scopes"/>, each a whole <c>scope</c> value.
    /// </summary>
    public Task<string> FetchTokenAsync(params string[] scopes) =>
        FetchTokenAsAsync(UnitTestAuthenticationPolicy.BasicAuthorization, scopes);

    /// <summary>
    /// Fetches a token with <paramref name="authorization"/> (a whole <c>Authorization</c> field)
    /// for <paramref name="scopes"/>, each a whole <c>scope</c> value.
    /// </summary>
    public async Task<string> FetchTokenAsAsync(string authorization, params string[] scopes)
    {
        var query = string.Join('&', scopes.Select(scope => "scope=" + Uri.EscapeDataString(scope)));
        var answer = await ServeAsync($"GET /token?service=surl&{query} HTTP/1.1\r\nHost: registry.test\r\n{authorization}\r\nConnection: close\r\n\r\n");
        StringAssert.StartsWith(answer, "HTTP/1.1 200 OK\r\n");
        using var json = System.Text.Json.JsonDocument.Parse(answer[(answer.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..]);

        return json.RootElement.GetProperty("token").GetString()!;
    }

    // The request with a token for its first line's repository (and a mount's from) after that line.
    private async Task<string> WithTokenAsync(string request)
    {
        var lineEnd = request.IndexOf("\r\n", StringComparison.Ordinal);
        var target = OciRequestTarget.Parse(request[..lineEnd].Split(' ')[1]);
        var scopes = new List<string>();
        if (target.Name.Length > 0)
        {
            scopes.Add($"repository:{target.Name}:*");
        }

        if (OciQueryParameters.First(target.Query, "from") is { } from)
        {
            scopes.Add($"repository:{from}:pull");
        }

        var token = await FetchTokenAsync([.. scopes]);

        return request.Insert(lineEnd + 2, $"Authorization: Bearer {token}\r\n");
    }

    private async Task<string> ServeAsync(string request)
    {
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(request)], initialTlsSession: TlsSession);
        await Server.ServeAsync(connection, Context());

        return Encoding.UTF8.GetString(connection.WrittenBytes);
    }
}
