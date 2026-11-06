using Surl.HttpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>Reads a request's header fields by name, case-insensitively, and compares media types.</summary>
internal static class McpHttpFields
{
    /// <summary>The first value of a field.</summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The field's name.</param>
    /// <returns>The value, or <see langword="null" /> when the request has no such field.</returns>
    public static string? Field(HttpRequest request, string name) =>
        request.Fields.Where(field => field.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(field => field.Value).FirstOrDefault();

    /// <summary>Every value of a field, joined with commas.</summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The field's name.</param>
    /// <returns>The joined values, or <see langword="null" /> when the request has no such field.</returns>
    public static string? JoinedField(HttpRequest request, string name)
    {
        string[] values = [.. request.Fields.Where(field => field.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(field => field.Value)];
        return values.Length == 0 ? null : string.Join(',', values);
    }

    /// <summary>Whether a <c>Content-Type</c> names a media type, with any parameters.</summary>
    /// <param name="contentType">The field's value, or <see langword="null" />.</param>
    /// <param name="mediaType">The media type, such as <c>application/json</c>.</param>
    /// <returns><see langword="true" /> when the type and subtype match, case-insensitively.</returns>
    public static bool IsMediaType(string? contentType, string mediaType) =>
        contentType is not null && contentType.Split(';')[0].Trim().Equals(mediaType, StringComparison.OrdinalIgnoreCase);
}
