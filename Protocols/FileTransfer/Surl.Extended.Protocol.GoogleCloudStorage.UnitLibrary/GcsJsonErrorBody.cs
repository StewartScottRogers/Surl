namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Writes the JSON API's error body ("HTTP status and error codes"; ADR-0148, decision 5):
/// <c>{"error":{"code":…,"message":…,"errors":[{"message":…,"domain":…,"reason":…}]}}</c> on one line.
/// No client byte is echoed, and no <c>location</c> is sent.
/// </summary>
internal static class GcsJsonErrorBody
{
    /// <summary>
    /// Renders the body.
    /// </summary>
    /// <param name="error">The error, whose message and reason are fixed text.</param>
    /// <returns>The body's bytes.</returns>
    public static byte[] Render(GcsJsonError error) => GcsJson.Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartObject("error");
        writer.WriteNumber("code", error.Status.Code);
        writer.WriteString("message", error.Message);
        writer.WriteStartArray("errors");
        writer.WriteStartObject();
        writer.WriteString("message", error.Message);
        writer.WriteString("domain", error.Domain);
        writer.WriteString("reason", error.Reason);
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    });
}
