namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Reads one field of a request by name, as HTTP compares names: case-insensitively.
/// </summary>
internal static class GcsRequestFields
{
    /// <summary>
    /// The value of the first field named <paramref name="name"/>.
    /// </summary>
    /// <param name="fields">The request's fields, in the order received.</param>
    /// <param name="name">The field's name.</param>
    /// <returns>The value, or <see langword="null"/> when no field has the name.</returns>
    public static string? Value(IReadOnlyList<KeyValuePair<string, string>> fields, string name) =>
        fields.FirstOrDefault(field => field.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
}
