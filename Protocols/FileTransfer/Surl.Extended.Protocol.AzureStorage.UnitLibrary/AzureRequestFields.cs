namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Reads a request's header fields by name, case-insensitively.
/// </summary>
internal static class AzureRequestFields
{
    /// <summary>
    /// The first value of the field named <paramref name="name"/>, or null when it is absent.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <param name="name">The field's name.</param>
    /// <returns>The value, or null.</returns>
    public static string? Value(IReadOnlyList<KeyValuePair<string, string>> fields, string name) =>
        fields.FirstOrDefault(field => field.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
}
