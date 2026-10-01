namespace Surl.HttpMessage;

/// <summary>
/// One header field of a request head, as the client sent it (RFC 9112, section 5).
/// </summary>
public sealed class HttpRequestField
{
    /// <summary>
    /// Creates a field.
    /// </summary>
    /// <param name="name">The field name, as sent.</param>
    /// <param name="value">The field value, with leading and trailing whitespace removed.</param>
    public HttpRequestField(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        Name = name;
        Value = value;
    }

    /// <summary>
    /// The field name, in the case the client sent it.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The field value, with the optional whitespace around it removed. Bytes above 0x7F
    /// are kept one character per byte (Latin-1).
    /// </summary>
    public string Value { get; }
}
