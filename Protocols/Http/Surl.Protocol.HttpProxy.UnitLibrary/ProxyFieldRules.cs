using System.Globalization;
using Surl.HttpMessage;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The header fields a proxy removes and adds as a message passes through it (ADR-0088
/// decision 3; RFC 9110 sections 7.6.1 to 7.6.3).
/// </summary>
internal static class ProxyFieldRules
{
    /// <summary>The <c>Via</c> value the proxy adds in both directions: <c>1.1 surl</c>, no version (ADR-0006 section 3).</summary>
    public const string ViaValue = "1.1 " + HttpResponseHead.ServerName;

    // Hop-by-hop in both directions. Transfer-Encoding is removed because the proxy writes
    // the body's framing itself; Upgrade is kept only on an upgrade.
    private static readonly string[] HopByHopFields =
        ["Connection", "Proxy-Connection", "Keep-Alive", "TE", "Trailer", "Transfer-Encoding", "Upgrade"];

    private static readonly string[] RequestOnlyFields = ["Proxy-Authorization"];

    private static readonly string[] ResponseOnlyFields = ["Proxy-Authenticate", "Proxy-Authentication-Info"];

    /// <summary>
    /// Whether a message asks to switch protocols: an <c>Upgrade</c> field and a
    /// <c>Connection</c> field listing <c>upgrade</c> (RFC 9110 section 7.8).
    /// </summary>
    /// <param name="fields">The message's fields.</param>
    /// <returns><see langword="true"/> for an upgrade.</returns>
    public static bool IsUpgrade(IReadOnlyList<HttpRequestField> fields) =>
        fields.Any(field => IsNamed(field, "Upgrade")) && ListsToken(fields, "Connection", "upgrade");

    /// <summary>
    /// Whether any field named <paramref name="name"/> lists <paramref name="token"/> in its
    /// comma-separated value, compared without regard to case.
    /// </summary>
    /// <param name="fields">The message's fields.</param>
    /// <param name="name">The field's name.</param>
    /// <param name="token">The token looked for.</param>
    /// <returns><see langword="true"/> when the token is listed.</returns>
    public static bool ListsToken(IReadOnlyList<HttpRequestField> fields, string name, string token) =>
        fields.Where(field => IsNamed(field, name)).SelectMany(field => Tokens(field.Value))
            .Contains(token, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The fields of a request forwarded to the origin: every hop-by-hop field and every field
    /// <c>Connection</c> names removed, <c>Proxy-Authorization</c> removed, <c>Max-Forwards</c>
    /// decremented when asked (<c>OPTIONS</c> and <c>TRACE</c>), then <c>Via</c>,
    /// <c>Connection: Upgrade</c> and <c>Upgrade</c> for an upgrade, and
    /// <c>Transfer-Encoding: chunked</c> when the body is sent on chunked.
    /// </summary>
    /// <remarks>
    /// <c>Host</c> is generated from the absolute-form's authority, replacing the one received
    /// in place or leading the fields when there was none (RFC 9112 section 3.2.2's MUST).
    /// </remarks>
    /// <param name="fields">The request's fields, as curl sent them.</param>
    /// <param name="host">The <c>Host</c> value: the request target's authority.</param>
    /// <param name="decrementsMaxForwards">Whether a numeric <c>Max-Forwards</c> goes on one less.</param>
    /// <param name="sendsChunkedBody">Whether the body goes on chunked.</param>
    /// <returns>The forwarded fields, in order.</returns>
    public static List<HttpRequestField> ForwardedRequestFields(IReadOnlyList<HttpRequestField> fields, string host, bool decrementsMaxForwards, bool sendsChunkedBody)
    {
        var hostField = new HttpRequestField("Host", host);
        var forwarded = EndToEndFields(fields, RequestOnlyFields)
            .Select(field => IsNamed(field, "Host") ? hostField : field)
            .Select(field => decrementsMaxForwards ? DecrementMaxForwards(field) : field)
            .ToList();
        if (!forwarded.Contains(hostField))
        {
            forwarded.Insert(0, hostField);
        }

        forwarded.Add(new HttpRequestField("Via", ViaValue));
        AddUpgradeFields(fields, forwarded);
        if (sendsChunkedBody)
        {
            forwarded.Add(new HttpRequestField("Transfer-Encoding", "chunked"));
        }

        return forwarded;
    }

    /// <summary>
    /// The fields of an origin's response relayed to curl: every hop-by-hop field and every
    /// field <c>Connection</c> names removed, <c>Proxy-Authenticate</c> and
    /// <c>Proxy-Authentication-Info</c> removed, then <c>Via</c>, the upgrade's fields for a
    /// <c>101</c>, <c>Transfer-Encoding: chunked</c> when the body is relayed chunked, and
    /// <c>Connection: close</c> when curl's connection closes after it.
    /// </summary>
    /// <param name="fields">The origin's response fields.</param>
    /// <param name="relaysChunkedBody">Whether the body is relayed chunked.</param>
    /// <param name="closesClientConnection">Whether curl's connection closes after the response.</param>
    /// <returns>The relayed fields, in order.</returns>
    public static List<HttpRequestField> RelayedResponseFields(IReadOnlyList<HttpRequestField> fields, bool relaysChunkedBody, bool closesClientConnection)
    {
        var relayed = EndToEndFields(fields, ResponseOnlyFields).ToList();
        relayed.Add(new HttpRequestField("Via", ViaValue));
        AddUpgradeFields(fields, relayed);
        if (relaysChunkedBody)
        {
            relayed.Add(new HttpRequestField("Transfer-Encoding", "chunked"));
        }

        if (closesClientConnection)
        {
            relayed.Add(new HttpRequestField("Connection", "close"));
        }

        return relayed;
    }

    /// <summary>
    /// The fields the proxy's login check judges (ADR-0088 decision 5): the request's own fields,
    /// with each <c>Authorization</c> removed - it is the origin's login, not the proxy's - and
    /// each <c>Proxy-Authorization</c> renamed <c>Authorization</c>, so the same checks the HTTP
    /// server asks about <c>Authorization</c> judge the proxy's credentials.
    /// </summary>
    /// <param name="fields">The request's fields, as curl sent them.</param>
    /// <returns>The fields as the authentication session reads them, in order.</returns>
    public static KeyValuePair<string, string>[] ProxyLoginFields(IReadOnlyList<HttpRequestField> fields) =>
        fields.Where(field => !IsNamed(field, "Authorization"))
            .Select(field => new KeyValuePair<string, string>(IsNamed(field, "Proxy-Authorization") ? "Authorization" : field.Name, field.Value))
            .ToArray();

    /// <summary>
    /// The value of a <c>Max-Forwards</c> field (RFC 9110 section 7.6.2), or
    /// <see langword="null"/> when the request has none or its value is not a number.
    /// </summary>
    /// <param name="fields">The request's fields.</param>
    /// <returns>The value.</returns>
    public static long? MaxForwards(IReadOnlyList<HttpRequestField> fields)
    {
        var field = fields.FirstOrDefault(candidate => IsNamed(candidate, "Max-Forwards"));

        return field is not null && long.TryParse(field.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static IEnumerable<HttpRequestField> EndToEndFields(IReadOnlyList<HttpRequestField> fields, string[] directionalFields)
    {
        var connectionNamed = fields.Where(field => IsNamed(field, "Connection")).SelectMany(field => Tokens(field.Value));
        var removed = new HashSet<string>(HopByHopFields.Concat(directionalFields).Concat(connectionNamed), StringComparer.OrdinalIgnoreCase);

        return fields.Where(field => !removed.Contains(field.Name));
    }

    private static void AddUpgradeFields(IReadOnlyList<HttpRequestField> fields, List<HttpRequestField> target)
    {
        if (IsUpgrade(fields))
        {
            target.Add(new HttpRequestField("Connection", "Upgrade"));
            target.AddRange(fields.Where(field => IsNamed(field, "Upgrade")));
        }
    }

    private static HttpRequestField DecrementMaxForwards(HttpRequestField field) =>
        IsNamed(field, "Max-Forwards") && long.TryParse(field.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? new HttpRequestField(field.Name, Math.Max(value - 1, 0).ToString(CultureInfo.InvariantCulture))
            : field;

    private static bool IsNamed(HttpRequestField field, string name) =>
        string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> Tokens(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
