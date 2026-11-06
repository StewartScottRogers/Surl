using System.Security;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The stream-level text the server writes (RFC 6120 sections 4.3, 4.4, 4.7 and 4.9; ADR-0114
/// decisions 1 and 6), always with the <c>stream</c> prefix the response header declares.
/// </summary>
internal static class XmppStreamText
{
    /// <summary>The digits of a stream ID: 128 random bits as lower-case hexadecimal (decision 1).</summary>
    public const int StreamIdHexDigits = 32;

    /// <summary>The language a response header names when the client named none (4.7.4).</summary>
    public const string DefaultLanguage = "en";

    /// <summary>The server's half of closing the stream (4.4).</summary>
    public const string Close = "</stream:stream>";

    /// <summary>The STARTTLS feature when TLS is required before anything else (5.3.1, 5.4.1).</summary>
    public const string StartTlsRequired = $"<starttls xmlns='{XmppNamespaces.Tls}'><required/></starttls>";

    /// <summary>The STARTTLS feature when it is voluntary, under <c>--allow-plaintext-auth</c> (ADR-0114 decision 2).</summary>
    public const string StartTlsOffered = $"<starttls xmlns='{XmppNamespaces.Tls}'/>";

    /// <summary>The answer to <c>&lt;starttls/&gt;</c> when the server will upgrade (5.4.2.3).</summary>
    public const string Proceed = $"<proceed xmlns='{XmppNamespaces.Tls}'/>";

    /// <summary>The answer to <c>&lt;starttls/&gt;</c> when the server cannot upgrade, then the close (5.4.2.2).</summary>
    public const string TlsFailureAndClose = $"<failure xmlns='{XmppNamespaces.Tls}'/>{Close}";

    /// <summary>
    /// The stream features element (4.3.2): <c>&lt;stream:features/&gt;</c> when there are none.
    /// </summary>
    /// <param name="features">The feature elements, in order; empty for none.</param>
    /// <returns>The features element.</returns>
    public static string Features(string features) =>
        features.Length == 0 ? "<stream:features/>" : $"<stream:features>{features}</stream:features>";

    /// <summary>
    /// The SASL feature (6.4.1): one <c>&lt;mechanism/&gt;</c> per name, in order, or nothing when
    /// no mechanism is offered.
    /// </summary>
    /// <param name="mechanisms">The mechanisms' registered names.</param>
    /// <returns>The <c>&lt;mechanisms/&gt;</c> element, or the empty string.</returns>
    public static string Mechanisms(IReadOnlyList<string> mechanisms) =>
        mechanisms.Count == 0
            ? string.Empty
            : $"<mechanisms xmlns='{XmppNamespaces.Sasl}'>{string.Concat(mechanisms.Select(name => $"<mechanism>{Escaped(name)}</mechanism>"))}</mechanisms>";

    /// <summary>
    /// The response header (4.7), preceded by an XML declaration: <c>from</c>, <c>id</c>,
    /// <c>version='1.0'</c>, <c>xml:lang</c>, the default namespace <c>jabber:client</c> and the
    /// stream namespace, in the order RFC 6120 section 9.1.1's example writes them, on one line.
    /// </summary>
    /// <param name="from">The stream's domain, or <see langword="null"/> to leave <c>from</c> out.</param>
    /// <param name="id">The stream ID.</param>
    /// <param name="language">The <c>xml:lang</c>.</param>
    /// <returns>The declaration and header.</returns>
    public static string ResponseHeader(string? from, string id, string language)
    {
        var fromAttribute = from is null ? string.Empty : $"from='{Escaped(from)}' ";

        return $"<?xml version='1.0'?><stream:stream {fromAttribute}id='{Escaped(id)}' version='1.0' xml:lang='{Escaped(language)}' " +
            $"xmlns='{XmppNamespaces.Client}' xmlns:stream='{XmppNamespaces.Streams}'>";
    }

    /// <summary>
    /// A stream error (4.9.2) followed by the close, with no <c>&lt;text/&gt;</c> (decision 6).
    /// </summary>
    /// <param name="condition">The condition, one of <see cref="XmppStreamErrors"/>.</param>
    /// <returns>The error and the close.</returns>
    public static string ErrorAndClose(string condition) =>
        $"<stream:error><{condition} xmlns='{XmppNamespaces.StreamErrors}'/></stream:error>{Close}";

    private static string Escaped(string value) => SecurityElement.Escape(value);
}
