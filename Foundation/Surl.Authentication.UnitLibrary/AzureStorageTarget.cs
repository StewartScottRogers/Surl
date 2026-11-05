namespace Surl.Authentication;

/// <summary>
/// A request target as Azure Storage's signatures read it: the path as sent, and each query
/// parameter's name and value percent-decoded, in the order sent ("Authorize with Shared Key",
/// "Constructing the canonicalized resource string"), a <c>+</c> read as a space.
/// </summary>
/// <param name="RawPath">The path, as sent, before any <c>?</c>.</param>
/// <param name="Parameters">Each query parameter, name and value percent-decoded.</param>
internal sealed record AzureStorageTarget(string RawPath, IReadOnlyList<KeyValuePair<string, string>> Parameters)
{
    /// <summary>
    /// Splits <paramref name="target"/> into its path and decoded query parameters. An empty
    /// parameter between two <c>&amp;</c> is skipped; one without <c>=</c> has the empty value.
    /// </summary>
    /// <param name="target">The request target as received.</param>
    /// <returns>The path and parameters.</returns>
    public static AzureStorageTarget Parse(string target)
    {
        var question = target.IndexOf('?', StringComparison.Ordinal);
        if (question < 0)
        {
            return new AzureStorageTarget(target, []);
        }

        var parameters = target[(question + 1)..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseParameter)
            .ToList();

        return new AzureStorageTarget(target[..question], parameters);
    }

    /// <summary>
    /// The first value of the parameter named exactly <paramref name="name"/>, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The parameter's name, compared as sent.</param>
    /// <returns>The decoded value, or <see langword="null"/>.</returns>
    public string? Value(string name) =>
        Parameters.FirstOrDefault(parameter => parameter.Key == name).Value;

    private static KeyValuePair<string, string> ParseParameter(string parameter)
    {
        var equals = parameter.IndexOf('=', StringComparison.Ordinal);

        return equals < 0
            ? new(Decode(parameter), string.Empty)
            : new(Decode(parameter[..equals]), Decode(parameter[(equals + 1)..]));
    }

    // A '+' is a space, as the Azure server's own query reading takes it.
    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
}
