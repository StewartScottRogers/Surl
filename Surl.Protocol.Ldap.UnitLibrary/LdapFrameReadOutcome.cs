namespace Surl.Protocol.Ldap;

/// <summary>
/// How reading one <c>LDAPMessage</c> off a connection ended (RFC 4511, section 5.1).
/// </summary>
internal enum LdapFrameReadOutcome
{
    /// <summary>A whole message was read.</summary>
    FrameRead,

    /// <summary>The client closed the connection between messages.</summary>
    ConnectionClosed,

    /// <summary>The client closed the connection part way through a message.</summary>
    ConnectionClosedMidMessage,

    /// <summary>The first byte was not the universal <c>SEQUENCE</c> tag (0x30) every <c>LDAPMessage</c> starts with.</summary>
    NotASequence,

    /// <summary>The message used the indefinite length form, which RFC 4511 section 5.1 forbids.</summary>
    IndefiniteLength,

    /// <summary>The length announced more than four length octets, more than any message this reader can hold needs.</summary>
    MalformedLength,

    /// <summary>The length announced a message longer than the message limit; none of its value was read.</summary>
    MessageTooLarge,
}
