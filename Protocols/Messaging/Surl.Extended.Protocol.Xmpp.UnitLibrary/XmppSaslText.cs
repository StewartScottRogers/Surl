namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The SASL and resource-binding text the server writes (RFC 6120 sections 6.4, 6.5 and 7;
/// ADR-0114 decisions 3 and 4), each element in its own namespace.
/// </summary>
internal static class XmppSaslText
{
    /// <summary>The feature a stream offers after SASL success, alone (decision 2).</summary>
    public const string BindFeature = $"<bind xmlns='{XmppNamespaces.Bind}'/>";

    /// <summary>
    /// A SASL challenge (6.4.3): the data as base64, or an empty element when there is none.
    /// </summary>
    /// <param name="data">The challenge's bytes.</param>
    /// <returns>The <c>&lt;challenge/&gt;</c> element.</returns>
    public static string Challenge(ReadOnlyMemory<byte> data) => Element("challenge", data);

    /// <summary>
    /// SASL success (6.4.6), carrying the additional data as base64 (6.3.10), or empty when there is none.
    /// </summary>
    /// <param name="data">The additional data with success, such as SCRAM's server-final message.</param>
    /// <returns>The <c>&lt;success/&gt;</c> element.</returns>
    public static string Success(ReadOnlyMemory<byte> data) => Element("success", data);

    /// <summary>
    /// A SASL failure (6.5) with no <c>&lt;text/&gt;</c> (decision 3).
    /// </summary>
    /// <param name="condition">The condition, one of <see cref="XmppSaslFailures"/>.</param>
    /// <returns>The <c>&lt;failure/&gt;</c> element.</returns>
    public static string Failure(string condition) => $"<failure xmlns='{XmppNamespaces.Sasl}'><{condition}/></failure>";

    /// <summary>
    /// The answer to a bind (7.6.1): the full JID the stream is bound to.
    /// </summary>
    /// <param name="id">The request's <c>id</c>.</param>
    /// <param name="fullJid">The full JID.</param>
    /// <returns>The result IQ.</returns>
    public static string BindResult(string id, string fullJid) =>
        $"<iq type='result' id='{Escaped(id)}'><bind xmlns='{XmppNamespaces.Bind}'><jid>{Escaped(fullJid)}</jid></bind></iq>";

    /// <summary>
    /// An empty IQ result (8.2.3), the answer to RFC 3921's legacy session request (decision 4).
    /// </summary>
    /// <param name="id">The request's <c>id</c>.</param>
    /// <returns>The result IQ.</returns>
    public static string EmptyResult(string id) => $"<iq type='result' id='{Escaped(id)}'/>";

    /// <summary>
    /// An IQ error (8.3), with no <c>&lt;text/&gt;</c>.
    /// </summary>
    /// <param name="id">The request's <c>id</c>.</param>
    /// <param name="type">The error type: <c>modify</c>, <c>cancel</c> or <c>wait</c>.</param>
    /// <param name="condition">The stanza error condition.</param>
    /// <returns>The error IQ.</returns>
    public static string IqError(string id, string type, string condition) =>
        $"<iq type='error' id='{Escaped(id)}'><error type='{type}'><{condition} xmlns='{XmppNamespaces.Stanzas}'/></error></iq>";

    private static string Element(string name, ReadOnlyMemory<byte> data) =>
        data.IsEmpty ? $"<{name} xmlns='{XmppNamespaces.Sasl}'/>" : $"<{name} xmlns='{XmppNamespaces.Sasl}'>{Convert.ToBase64String(data.Span)}</{name}>";

    private static string Escaped(string value) => System.Security.SecurityElement.Escape(value);
}
