namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The SASL failure conditions the server sends (RFC 6120 section 6.5; ADR-0114 decision 3), each
/// the local name of an element inside <c>&lt;failure/&gt;</c> in <see cref="XmppNamespaces.Sasl"/>.
/// </summary>
public static class XmppSaslFailures
{
    /// <summary>The client's <c>&lt;abort/&gt;</c> (6.5.1); never counted as a failed attempt.</summary>
    public const string Aborted = "aborted";

    /// <summary>A mechanism needing TLS before TLS while it is required (6.5.4).</summary>
    public const string EncryptionRequired = "encryption-required";

    /// <summary>Base64 that does not decode (6.5.5).</summary>
    public const string IncorrectEncoding = "incorrect-encoding";

    /// <summary>A mechanism not offered on this connection (6.5.7).</summary>
    public const string InvalidMechanism = "invalid-mechanism";

    /// <summary>An <c>&lt;auth/&gt;</c> naming no mechanism (6.5.8).</summary>
    public const string MalformedRequest = "malformed-request";

    /// <summary>Credentials refused, or an account unusable as a JID; never says which (6.5.10).</summary>
    public const string NotAuthorized = "not-authorized";
}
