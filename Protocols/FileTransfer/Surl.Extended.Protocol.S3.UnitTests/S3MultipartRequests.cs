using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The multipart requests, written from the API Reference's CreateMultipartUpload, UploadPart,
/// CompleteMultipartUpload and AbortMultipartUpload pages, sent through an <see cref="S3ServerHarness"/>.
/// </summary>
internal static partial class S3MultipartRequests
{
    /// <summary>
    /// 5 MiB, the smallest part but the last.
    /// </summary>
    public const int FiveMebibytes = 5 * 1024 * 1024;

    public static Task<string> CreateAsync(S3ServerHarness harness, string path, params string[] fields) =>
        harness.SendAsync("POST", $"{path}?uploads", fields);

    /// <summary>
    /// Creates an upload and returns its ID, checked to be 32 lower-case hexadecimal digits.
    /// </summary>
    public static async Task<string> CreateUploadIdAsync(S3ServerHarness harness, string path, params string[] fields)
    {
        var response = await CreateAsync(harness, path, fields);
        var match = UploadIdElement().Match(response);
        Assert.IsTrue(match.Success, response);

        return match.Groups[1].Value;
    }

    public static Task<string> UploadPartAsync(S3ServerHarness harness, string path, string uploadId, int partNumber, string body, params string[] fields) =>
        harness.SendWithBodyAsync("PUT", $"{path}?partNumber={partNumber}&uploadId={uploadId}", body, fields);

    public static Task<string> CompleteAsync(S3ServerHarness harness, string path, string uploadId, string body, params string[] fields) =>
        harness.SendWithBodyAsync("POST", $"{path}?uploadId={uploadId}", body, fields);

    public static Task<string> AbortAsync(S3ServerHarness harness, string path, string uploadId) =>
        harness.SendAsync("DELETE", $"{path}?uploadId={uploadId}");

    /// <summary>
    /// A CompleteMultipartUpload body naming each part with its number and ETag, as the page's example does.
    /// </summary>
    public static string CompletionBody(params (int PartNumber, string ETag)[] parts) =>
        "<CompleteMultipartUpload xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">"
            + string.Concat(parts.Select(part => $"<Part><ETag>{part.ETag}</ETag><PartNumber>{part.PartNumber}</PartNumber></Part>"))
            + "</CompleteMultipartUpload>";

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

    [GeneratedRegex("\r\nETag: (\"[^\"]*\")\r\n")]
    private static partial Regex ETagField();
}
