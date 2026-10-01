namespace Surl.Protocol.Imap;

/// <summary>
/// A body part's media type (RFC 2045, section 5): the type and subtype in capitals, and the
/// parameters.
/// </summary>
/// <param name="Type">The type in capitals, such as <c>TEXT</c>.</param>
/// <param name="Subtype">The subtype in capitals, such as <c>PLAIN</c>.</param>
/// <param name="Parameters">The parameters, names in capitals.</param>
internal sealed record ImapContentType(string Type, string Subtype, IReadOnlyList<KeyValuePair<string, string>> Parameters)
{
    /// <summary>
    /// The type of a part with no <c>Content-Type</c> (RFC 2045, section 5.2), and of a
    /// multipart whose structure does not parse (ADR-0055, decision 4).
    /// </summary>
    public static readonly ImapContentType TextPlain = new("TEXT", "PLAIN", [new("CHARSET", "US-ASCII")]);

    /// <summary>
    /// The type of a <c>multipart/digest</c>'s part with no <c>Content-Type</c> (RFC 2046,
    /// section 5.1.5).
    /// </summary>
    public static readonly ImapContentType MessageRfc822 = new("MESSAGE", "RFC822", []);

    /// <summary>
    /// Whether it is a <c>multipart</c> type.
    /// </summary>
    public bool IsMultipart => Type == "MULTIPART";

    /// <summary>
    /// Whether it is <c>message/rfc822</c>, a part that holds a whole message.
    /// </summary>
    public bool IsMessage => Type == "MESSAGE" && Subtype == "RFC822";

    /// <summary>
    /// Whether it is a <c>text</c> type, whose body structure counts its lines.
    /// </summary>
    public bool IsText => Type == "TEXT";

    /// <summary>
    /// The type a <c>Content-Type</c> field gives.
    /// </summary>
    /// <param name="field">The field, or <see langword="null"/> when the part has none.</param>
    /// <returns>The type, or <see langword="null"/> when there is no field or its value is not
    /// <c>type/subtype</c>.</returns>
    public static ImapContentType? Read(ImapHeaderField? field)
    {
        var value = field is null ? null : ImapMimeValue.Parse(field.Text);
        var slash = value?.Value.IndexOf('/', StringComparison.Ordinal) ?? -1;
        if (slash <= 0 || slash == value!.Value.Length - 1)
        {
            return null;
        }

        return new ImapContentType(value.Value[..slash].Trim().ToUpperInvariant(), value.Value[(slash + 1)..].Trim().ToUpperInvariant(), value.Parameters);
    }

    /// <summary>
    /// The value of the first parameter named <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The parameter name in capitals.</param>
    /// <returns>Its value, or <see langword="null"/> when there is none.</returns>
    public string? Parameter(string name) => new ImapMimeValue(string.Empty, Parameters).Parameter(name);
}
