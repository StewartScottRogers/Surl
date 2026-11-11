using System.Text;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// What a JSON API answer is expected to be, written from the JSON API reference pages and ADR-0148: the status, the
/// common fields, <c>Content-Type</c>, <c>Content-Length</c> and the one-line body.
/// </summary>
internal static class GcsJsonServer
{
    public const string HelloWorldMd5 = "XrY7u+Ae7tCTyyK7j1rNww==";

    public const string HelloWorldCrc32c = "yZRlqg==";

    public const string HelloWorldETag = "\"5eb63bbbe01eeed093cb22bb8f5acdc3\"";

    public static string Json(string body, string status = "200 OK", string extraFields = "") =>
        $"HTTP/1.1 {status}\r\n{GcsServerHarness.CommonFields}{extraFields}Content-Type: application/json; charset=UTF-8\r\n"
        + $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";

    public static string JsonError(string status, string message, string reason, string extraFields = "", string domain = "global")
    {
        var code = status[..3];

        return Json(
            $"{{\"error\":{{\"code\":{code},\"message\":\"{message}\",\"errors\":[{{\"message\":\"{message}\",\"domain\":\"{domain}\",\"reason\":\"{reason}\"}}]}}}}",
            status,
            extraFields);
    }

    public static string NotImplemented => JsonError("501 Not Implemented", "Not implemented.", "notImplemented");

    public static string NotFound => JsonError("404 Not Found", "Not Found", "notFound");

    public static string ETagOf(string generation, string metageneration) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{generation}/{metageneration}"));

    /// <summary>
    /// The resource of <c>photos/a.txt</c> holding "hello world", as the harness writes it: generation and times the harness clock.
    /// </summary>
    public static string HelloObject(string name = "a.txt", string link = "a.txt", string extraMembers = "", string metageneration = "1", string contentType = "text/plain", string origin = "http://127.0.0.1:4443") =>
        $"{{\"kind\":\"storage#object\",\"id\":\"photos/{name}/{GcsServerHarness.NowGeneration}\",\"selfLink\":\"{origin}/storage/v1/b/photos/o/{link}\","
        + $"\"mediaLink\":\"{origin}/download/storage/v1/b/photos/o/{link}?generation={GcsServerHarness.NowGeneration}&alt=media\","
        + $"\"name\":\"{name}\",\"bucket\":\"photos\",\"generation\":\"{GcsServerHarness.NowGeneration}\",\"metageneration\":\"{metageneration}\","
        + $"\"contentType\":\"{contentType}\",\"storageClass\":\"STANDARD\",\"size\":\"11\",\"md5Hash\":\"{HelloWorldMd5}\",\"crc32c\":\"{HelloWorldCrc32c}\","
        + $"\"etag\":\"{ETagOf(GcsServerHarness.NowGeneration, metageneration)}\",\"timeCreated\":\"{GcsServerHarness.NowListTime}\","
        + $"\"updated\":\"{GcsServerHarness.NowListTime}\",\"timeStorageClassUpdated\":\"{GcsServerHarness.NowListTime}\"{extraMembers}}}";

    /// <summary>
    /// A harness with the bucket <c>photos</c> holding <c>a.txt</c>, "hello world".
    /// </summary>
    public static GcsServerHarness HarnessWithHello(Surl.Content.ContentExposureOptions? exposure = null, bool allowsAnonymous = false)
    {
        var harness = new GcsServerHarness(allowsAnonymous: allowsAnonymous, exposure: exposure ?? GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        return harness;
    }
}
