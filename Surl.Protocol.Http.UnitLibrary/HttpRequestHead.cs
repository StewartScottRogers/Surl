namespace Surl.Protocol.Http;

/// <summary>
/// A request head: the request line and the header fields that follow it (RFC 9112,
/// sections 3 and 5).
/// </summary>
public sealed class HttpRequestHead
{
    private readonly HttpRequestField[] fields;

    /// <summary>
    /// Creates a request head.
    /// </summary>
    /// <param name="method">The method token, case-sensitive, as sent.</param>
    /// <param name="requestTarget">The request target, raw: not percent-decoded and not normalized.</param>
    /// <param name="version">The protocol version the server treats the request as.</param>
    /// <param name="fields">The header fields, in the order sent.</param>
    public HttpRequestHead(string method, string requestTarget, Version version, IEnumerable<HttpRequestField> fields)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(requestTarget);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(fields);

        Method = method;
        RequestTarget = requestTarget;
        Version = version;
        this.fields = fields.ToArray();
    }

    /// <summary>
    /// The method token, case-sensitive, as sent (<c>GET</c>, <c>HEAD</c>).
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// The request target exactly as sent: not percent-decoded, dot segments not removed.
    /// </summary>
    public string RequestTarget { get; }

    /// <summary>
    /// The protocol version the request is treated as: 1.0, or 1.1 for HTTP/1.1 and any higher HTTP/1.x.
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
