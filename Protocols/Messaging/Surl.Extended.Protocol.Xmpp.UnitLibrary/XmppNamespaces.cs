namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The XML namespaces an XMPP client-to-server stream uses (RFC 6120 sections 4.8 and 4.9.3).
/// </summary>
public static class XmppNamespaces
{
    /// <summary>The stream namespace, of <c>&lt;stream:stream&gt;</c>, <c>&lt;stream:features&gt;</c> and <c>&lt;stream:error&gt;</c>.</summary>
    public const string Streams = "http://etherx.jabber.org/streams";

    /// <summary>The content namespace of a client-to-server stream, the default namespace of its stanzas.</summary>
    public const string Client = "jabber:client";

    /// <summary>The namespace of a stream error's condition element.</summary>
    public const string StreamErrors = "urn:ietf:params:xml:ns:xmpp-streams";

    /// <summary>The namespace the <c>xml:</c> prefix is bound to, of <c>xml:lang</c>.</summary>
    public const string Xml = "http://www.w3.org/XML/1998/namespace";

    /// <summary>The namespace namespace declarations (<c>xmlns</c>, <c>xmlns:p</c>) are read in.</summary>
    public const string XmlnsDeclarations = "http://www.w3.org/2000/xmlns/";
}
