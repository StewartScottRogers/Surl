using System.Text;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Renders the distribution specification's error body, one line of UTF-8 JSON with no
/// <c>detail</c> (Dist "Error Codes"; ADR-0115, decision 5.1):
/// <c>{"errors":[{"code":"MANIFEST_UNKNOWN","message":"manifest unknown"}]}</c>.
/// </summary>
internal static class OciErrorBody
{
    /// <summary>
    /// The body for <paramref name="error"/>. Codes and messages are fixed ASCII with nothing to
    /// escape, so they are written as they stand.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The body's bytes.</returns>
    public static byte[] Render(OciError error) => Render(error, 1);

    /// <summary>
    /// The body listing <paramref name="error"/> <paramref name="count"/> times, as a manifest push
    /// names one <c>MANIFEST_BLOB_UNKNOWN</c> per missing digest (ADR-0115, decision 3.1).
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="count">How many times it is listed, at least 1.</param>
    /// <returns>The body's bytes.</returns>
    public static byte[] Render(OciError error, int count)
    {
        var entry = $"{{\"code\":\"{error.Code}\",\"message\":\"{error.Message}\"}}";

        return Encoding.UTF8.GetBytes($"{{\"errors\":[{string.Join(',', Enumerable.Repeat(entry, count))}]}}");
    }
}
