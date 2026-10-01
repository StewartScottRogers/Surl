namespace Surl.HttpMessage;

/// <summary>
/// A request head: the request line and the header fields that follow it (RFC 9112,
/// sections 3 and 5).
/// </summary>
public sealed class HttpRequestHead
{
    private readonly HttpRequestField[] fields;

    /// <summary>
    /// Creates an HTTP request head: one whose request line named <see cref="HttpMessageProtocol.Http11"/>.
    /// </summary>
    /// <param name="method">The method token, case-sensitive, as sent.</param>
    /// <param name="requestTarget">The request target, raw: not percent-decoded and not normalized.</param>
    /// <param name="version">The protocol version the server treats the request as.</param>
    /// <param name="fields">The header fields, in the order sent.</param>
    public HttpRequestHead(string method, string requestTarget, Version version, IEnumerable<HttpRequestField> fields)
        : this(HttpMessageProtocol.Http11, method, requestTarget, version, fields)
    {
    }

    /// <summary>
    /// Creates a request head whose request line named <paramref name="protocol"/>.
    /// </summary>
    /// <param name="protocol">The protocol the request line's version named.</param>
    /// <param name="method">The method token, case-sensitive, as sent.</param>
    /// <param name="requestTarget">The request target, raw: not percent-decoded and not normalized.</param>
    /// <param name="version">The protocol version the server treats the request as.</param>
    /// <param name="fields">The header fields, in the order sent.</param>
    public HttpRequestHead(HttpMessageProtocol protocol, string method, string requestTarget, Version version, IEnumerable<HttpRequestField> fields)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(requestTarget);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(fields);

        Protocol = protocol;
        Method = method;
        RequestTarget = requestTarget;
        Version = version;
        this.fields = fields.ToArray();
    }

    /// <summary>
    /// The protocol the request line's version named: <see cref="HttpMessageProtocol.Http11"/>
    /// for <c>HTTP/1.x</c>, <see cref="HttpMessageProtocol.Rtsp10"/> for <c>RTSP/1.x</c>.
    /// </summary>
    public HttpMessageProtocol Protocol { get; }

    /// <summary>
    /// The method token, case-sensitive, as sent (<c>GET</c>, <c>HEAD</c>).
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// The request target exactly as sent: not percent-decoded, dot segments not removed.
    /// </summary>
    public string RequestTarget { get; }

    /// <summary>
    /// The protocol version the request is treated as: the 1.x it named, or
    /// <see cref="HttpMessageProtocol.HighestMinorVersion"/> for any higher 1.x - 1.0 or 1.1
    /// for HTTP, 1.0 for RTSP.
    /// </summary>
    public Version Version { get; }

    /// <summary>
    /// The header fields, in the order sent, repeated names included.
    /// </summary>
    public IReadOnlyList<HttpRequestField> Fields => Array.AsReadOnly(fields);

    /// <summary>
    /// Returns the values of every field named <paramref name="name"/>, compared without
    /// regard to ASCII case, in the order sent.
    /// </summary>
    /// <param name="name">The field name to look up.</param>
    /// <returns>The values in order; empty when no field has that name.</returns>
    public IReadOnlyList<string> GetFieldValues(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return fields
            .Where(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(field => field.Value)
            .ToArray();
    }
}
