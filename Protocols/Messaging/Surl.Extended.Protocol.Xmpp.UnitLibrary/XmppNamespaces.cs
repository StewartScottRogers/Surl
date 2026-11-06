namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The XML namespaces an XMPP client-to-server stream uses (RFC 6120 sections 4.8, 4.9.3, 5 and 6).
/// </summary>
public static class XmppNamespaces
{
    /// <summary>The stream namespace, of <c>&lt;stream:stream&gt;</c>, <c>&lt;stream:features&gt;</c> and <c>&lt;stream:error&gt;</c>.</summary>
    public const string Streams = "http://etherx.jabber.org/streams";

    /// <summary>The content namespace of a client-to-server stream, the default namespace of its stanzas.</summary>
    public const string Client = "jabber:client";

    /// <summary>The namespace of a stream error's condition element.</summary>
    public const string StreamErrors = "urn:ietf:params:xml:ns:xmpp-streams";

    /// <summary>The STARTTLS namespace, of <c>&lt;starttls/&gt;</c>, <c>&lt;proceed/&gt;</c> and its <c>&lt;failure/&gt;</c> (5.4).</summary>
    public const string Tls = "urn:ietf:params:xml:ns:xmpp-tls";

    /// <summary>The SASL namespace, of <c>&lt;mechanisms/&gt;</c>, <c>&lt;auth/&gt;</c> and its <c>&lt;failure/&gt;</c> (6.4).</summary>
    public const string Sasl = "urn:ietf:params:xml:ns:xmpp-sasl";

    /// <summary>The resource binding namespace, of the <c>&lt;bind/&gt;</c> feature and request (7).</summary>
    public const string Bind = "urn:ietf:params:xml:ns:xmpp-bind";

    /// <summary>The namespace of a stanza error's condition element (8.3.3).</summary>
    public const string Stanzas = "urn:ietf:params:xml:ns:xmpp-stanzas";

    /// <summary>RFC 3921's legacy session namespace, which RFC 6121 appendix E lets a server still answer.</summary>
    public const string Session = "urn:ietf:params:xml:ns:xmpp-session";

    /// <summary>The namespace the <c>xml:</c> prefix is bound to, of <c>xml:lang</c>.</summary>
    public const string Xml = "http://www.w3.org/XML/1998/namespace";

    /// <summary>The namespace namespace declarations (<c>xmlns</c>, <c>xmlns:p</c>) are read in.</summary>
    public const string XmlnsDeclarations = "http://www.w3.org/2000/xmlns/";
}
