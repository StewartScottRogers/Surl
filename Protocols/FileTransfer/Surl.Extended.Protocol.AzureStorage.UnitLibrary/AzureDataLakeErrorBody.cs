using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Writes the Data Lake endpoint's error body, the <c>DataLakeStorageError</c> each Data Lake page
/// defines (ADR-0109, decision 5.2): <c>{"error":{"code":…,"message":…}}</c>, the message the code's
/// fixed text, a <c>RequestId:</c> line and a <c>Time:</c> line. No client byte is echoed.
/// </summary>
internal static class AzureDataLakeErrorBody
{
    /// <summary>
    /// The <c>Content-Type</c> the body is sent with.
    /// </summary>
    public const string ContentType = "application/json;charset=utf-8";

    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Renders the body.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="requestId">The answer's <c>x-ms-request-id</c>.</param>
    /// <param name="now">When the answer is made, written on the <c>Time:</c> line.</param>
    /// <returns>The body's UTF-8 bytes.</returns>
    public static byte[] Render(AzureError error, string requestId, DateTimeOffset now)
    {
        var message = $"{error.Message}\nRequestId:{requestId}\nTime:{now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)}";
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, WriterOptions))
        {
            json.WriteStartObject();
            json.WriteStartObject("error");
            json.WriteString("code", error.Code);
            json.WriteString("message", message);
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return buffer.ToArray();
    }
}
