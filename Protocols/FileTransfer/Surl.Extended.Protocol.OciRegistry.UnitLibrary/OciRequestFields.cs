using Surl.HttpServing;

namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Reads a request's header fields by name, compared without regard to case (RFC 9110, section 5.1).
/// </summary>
internal static class OciRequestFields
{
    /// <summary>
    /// The value of the first field named <paramref name="name"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The field's name.</param>
    /// <returns>The value, or <see langword="null"/> when the request carries no such field.</returns>
    public static string? Value(HttpRequest request, string name) =>
        request.Fields.FirstOrDefault(field => string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}
