using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The multipart and resumable upload requests, written from the XML API pages "POST Object
/// multipart", "PUT Object multipart", "GET Object multipart", "POST Object complete multipart",
/// "DELETE Object multipart" and "Perform resumable uploads", sent with the Bearer token through a
/// <see cref="GcsServerHarness"/>.
/// </summary>
internal static partial class GcsUploadRequests
{
    /// <summary>
    /// 5 MiB, the smallest part but the last.
    /// </summary>
    public const int FiveMebibytes = 5 * 1024 * 1024;

    public static Task<string> InitiateAsync(GcsServerHarness harness, string path, params string[] fields) =>
        harness.SendWithBearerAsync("POST", $"{path}?uploads", ["Content-Length: 0", .. fields]);

    /// <summary>
    /// Initiates a multipart upload and returns its ID, checked to be 32 lower-case hexadecimal digits.
    /// </summary>
    public static async Task<string> InitiateUploadIdAsync(GcsServerHarness harness, string path, params string[] fields)
    {
        var response = await InitiateAsync(harness, path, fields);
        var match = UploadIdElement().Match(response);
        Assert.IsTrue(match.Success, response);

        return match.Groups[1].Value;
    }

    public static Task<string> UploadPartAsync(GcsServerHarness harness, string path, string uploadId, int partNumber, string body, params string[] fields) =>
        harness.SendWithBearerAndBodyAsync("PUT", $"{path}?partNumber={partNumber}&uploadId={uploadId}", body, fields);

    public static Task<string> CompleteAsync(GcsServerHarness harness, string path, string uploadId, string body) =>
        harness.SendWithBearerAndBodyAsync("POST", $"{path}?uploadId={uploadId}", body);

    public static Task<string> ListPartsAsync(GcsServerHarness harness, string path, string uploadId, string query = "") =>
        harness.SendWithBearerAsync("GET", $"{path}?uploadId={uploadId}{query}");

    public static Task<string> AbortAsync(GcsServerHarness harness, string path, string uploadId) =>
        harness.SendWithBearerAsync("DELETE", $"{path}?uploadId={uploadId}", "Content-Length: 0");

    /// <summary>
    /// A completion body naming each part with its number and ETag, as the page's example does.
    /// </summary>
    public static string CompletionBody(params (int PartNumber, string ETag)[] parts) =>
        "<CompleteMultipartUpload>"
            + string.Concat(parts.Select(part => $"<Part><PartNumber>{part.PartNumber}</PartNumber><ETag>{part.ETag}</ETag></Part>"))
            + "</CompleteMultipartUpload>";

    /// <summary>
    /// Starts a resumable upload and returns its session URI's <c>upload_id</c>.
    /// </summary>
    public static async Task<string> StartSessionAsync(GcsServerHarness harness, string path, params string[] fields)
    {
        var response = await harness.SendWithBearerAsync("POST", path, ["x-goog-resumable: start", "Content-Length: 0", .. fields]);
        var match = SessionUri().Match(response);
        Assert.IsTrue(match.Success, response);

        return match.Groups[1].Value;
    }

    public static Task<string> PutChunkAsync(GcsServerHarness harness, string path, string uploadId, string body, params string[] fields) =>
        harness.SendWithBearerAndBodyAsync("PUT", $"{path}?upload_id={uploadId}", body, fields);

    /// <summary>
    /// The quoted lower-case hexadecimal MD5 of <paramref name="text"/>'s UTF-8 bytes.
    /// </summary>
    public static string ETagOf(string text) => $"\"{Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(text)))}\"";

    /// <summary>
    /// The ETag an answer carries, quotes and all.
    /// </summary>
    public static string ETagFieldOf(string response)
    {
        var match = ETagField().Match(response);
        Assert.IsTrue(match.Success, response);

        return match.Groups[1].Value;
    }

    [GeneratedRegex("<UploadId>([0-9a-f]{32})</UploadId>")]
    private static partial Regex UploadIdElement();

    [GeneratedRegex("\r\nLocation: http://127\\.0\\.0\\.1:4443/[^?]*\\?upload_id=([0-9a-f]{32})\r\n")]
    private static partial Regex SessionUri();

    [GeneratedRegex("\r\nETag: (\"[^\"]*\")\r\n")]
    private static partial Regex ETagField();
}
