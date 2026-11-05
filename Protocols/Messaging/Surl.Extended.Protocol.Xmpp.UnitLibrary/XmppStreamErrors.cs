namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The stream error conditions the server sends (RFC 6120 section 4.9.3; ADR-0114 decision 6),
/// each the local name of an element in <see cref="XmppNamespaces.StreamErrors"/>.
/// </summary>
public static class XmppStreamErrors
{
    /// <summary>No stream header within the head timeout, or no byte within the idle timeout (4.9.3.4).</summary>
    public const string ConnectionTimeout = "connection-timeout";

    /// <summary>A <c>to</c> naming a domain the server does not serve (4.9.3.6).</summary>
    public const string HostUnknown = "host-unknown";

    /// <summary>An unexpected failure in the server; nothing about it follows (4.9.3.8).</summary>
    public const string InternalServerError = "internal-server-error";

    /// <summary>A stream namespace or content namespace other than the right ones (4.9.3.10).</summary>
    public const string InvalidNamespace = "invalid-namespace";

    /// <summary>A stanza before the client has authenticated and bound a resource (4.9.3.12).</summary>
    public const string NotAuthorized = "not-authorized";

    /// <summary>Bytes that are not well-formed XML, or a first element that is not a stream header (4.9.3.13).</summary>
    public const string NotWellFormed = "not-well-formed";

    /// <summary>An element the phase does not allow, or a limit passed (4.9.3.14; ADR-0114 decision 7).</summary>
    public const string PolicyViolation = "policy-violation";

    /// <summary>A connection over <c>--max-connections</c> or <c>--max-connections-per-address</c> (4.9.3.17).</summary>
    public const string ResourceConstraint = "resource-constraint";

    /// <summary>A DTD, comment, processing instruction or non-predefined entity reference (4.9.3.18; section 11.1).</summary>
    public const string RestrictedXml = "restricted-xml";

    /// <summary>The server is stopping (4.9.3.20).</summary>
    public const string SystemShutdown = "system-shutdown";

    /// <summary>An encoding other than UTF-8 (4.9.3.22; section 11.6).</summary>
    public const string UnsupportedEncoding = "unsupported-encoding";

    /// <summary>A first-level element in <c>jabber:client</c> that is not a stanza (4.9.3.24).</summary>
    public const string UnsupportedStanzaType = "unsupported-stanza-type";

    /// <summary>A stream header with no <c>version</c>, or a major version other than 1 (4.9.3.25).</summary>
    public const string UnsupportedVersion = "unsupported-version";
}
