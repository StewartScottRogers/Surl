namespace Surl.Protocol.Imap;

/// <summary>
/// A MIME field value with parameters (RFC 2045, section 5.1; RFC 2183): a value such as
/// <c>text/plain</c> or <c>attachment</c>, then <c>; name=value</c> pairs, the values plain or
/// quoted, comments removed.
/// </summary>
/// <param name="Value">The value before the first <c>;</c>, trimmed.</param>
/// <param name="Parameters">The parameters in order, each name in capitals and each value
/// unquoted. A piece without <c>=</c> or a name is left out.</param>
internal sealed record ImapMimeValue(string Value, IReadOnlyList<KeyValuePair<string, string>> Parameters)
{
    /// <summary>
    /// Reads a field value.
    /// </summary>
    /// <param name="text">The unfolded field value.</param>
    /// <returns>The value and its parameters.</returns>
    public static ImapMimeValue Parse(string text)
    {
        var pieces = ImapHeaderText.Split(ImapHeaderText.RemoveComments(text), ';');
        List<KeyValuePair<string, string>> parameters = [];
        foreach (var piece in pieces.Skip(1))
        {
            var equals = piece.IndexOf('=', StringComparison.Ordinal);
            var name = equals < 0 ? string.Empty : piece[..equals].Trim().ToUpperInvariant();
            if (name.Length > 0)
            {
                parameters.Add(new(name, ImapHeaderText.Unquote(piece[(equals + 1)..].Trim())));
            }
        }

        return new ImapMimeValue(pieces[0].Trim(), parameters);
    }

    /// <summary>
    /// The value of the first parameter named <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The parameter name in capitals.</param>
    /// <returns>Its value, or <see langword="null"/> when there is none.</returns>
    public string? Parameter(string name) =>
        Parameters.Where(parameter => parameter.Key == name).Select(parameter => parameter.Value).FirstOrDefault();
}
