namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// A request's query parameters, each name and value percent-decoded once.
/// </summary>
internal static class GcsQueryParameters
{
    /// <summary>
    /// Splits a query into its parameters; a parameter with no <c>=</c> has an empty value.
    /// </summary>
    /// <param name="query">The query after the <c>?</c>.</param>
    /// <returns>The parameters, in the order sent.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> Parse(string query) =>
        [.. query.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(ParseOne)];

    /// <summary>
    /// The value of the first parameter named <paramref name="name"/>.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="name">The name, compared exactly.</param>
    /// <returns>The value, or <see langword="null"/> when no parameter has the name.</returns>
    public static string? Value(IReadOnlyList<KeyValuePair<string, string>> parameters, string name) =>
        parameters.FirstOrDefault(parameter => parameter.Key == name).Value;

    private static KeyValuePair<string, string> ParseOne(string parameter)
    {
        var equals = parameter.IndexOf('=');

        return equals < 0
            ? new(Uri.UnescapeDataString(parameter), string.Empty)
            : new(Uri.UnescapeDataString(parameter[..equals]), Uri.UnescapeDataString(parameter[(equals + 1)..]));
    }
}
