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

    public OciRegistryServerHarness()
    {
        TimeProvider = new FixedTimeProvider(Now);
        FileSystem = new InMemoryContentFileSystem(TimeProvider);
        Store = new ContentStore(InMemoryContentFileSystem.RootPath, FileSystem, new ContentExposureOptions());
        Server = new OciRegistryProtocolServer(Store, TimeProvider);
    }

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

    public ExchangeContext Context() => new(
        1,
        new ListenUrl(OciRegistryProtocolServer.Scheme, "127.0.0.1", 5000).WithBoundPort(5000),
        new IPEndPoint(IPAddress.Loopback, 5000),
        new IPEndPoint(IPAddress.Loopback, 50000),
        Log,
        TimeProvider,
        CancellationToken.None);

    /// <summary>
    /// Sends one request head, <c>Host: registry.test</c> and <c>Connection: close</c> added, and
    /// returns what the server wrote.
    /// </summary>
    public Task<string> SendAsync(string method, string target, params string[] fields) =>
        SendRawAsync($"{method} {target} HTTP/1.1\r\nHost: registry.test\r\n{string.Concat(fields.Select(field => field + "\r\n"))}Connection: close\r\n\r\n");

    public async Task<string> SendRawAsync(string request)
    {
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(request)]);
        await Server.ServeAsync(connection, Context());

        return Encoding.UTF8.GetString(connection.WrittenBytes);
    }
}
