using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Writes a JSON body with <see cref="Utf8JsonWriter"/>: BCL, no reflection, members in the order written
/// (ADR-0148, decision 3), on one line, as UTF-8 with no escaping of printable characters.
/// </summary>
internal static class GcsJson
{
    /// <summary>
    /// The <c>Content-Type</c> of every JSON body the server writes.
    /// </summary>
    public const string ContentType = "application/json; charset=UTF-8";

    private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Writes a JSON document.
    /// </summary>
    /// <param name="write">Writes the document's one value.</param>
    /// <returns>The document's UTF-8 bytes.</returns>
    public static byte[] Write(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, Options))
        {
            write(writer);
        }

        return buffer.WrittenSpan.ToArray();
    }
}
