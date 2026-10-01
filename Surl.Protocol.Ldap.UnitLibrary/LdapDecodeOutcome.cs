namespace Surl.Protocol.Ldap;

/// <summary>
/// How decoding one <c>LDAPMessage</c> ended. Every outcome but <see cref="Decoded"/> is a
/// malformed message the server answers with <see cref="LdapResultCode.ProtocolError"/>
/// (RFC 4511, section 4.1.1).
/// </summary>
internal enum LdapDecodeOutcome
{
    /// <summary>The message decoded.</summary>
    Decoded,

    /// <summary>An element's tag or length does not fit the bytes that hold it.</summary>
    MalformedTagOrLength,

    /// <summary>An element used the indefinite length form, which RFC 4511 section 5.1 forbids.</summary>
    IndefiniteLength,

    /// <summary>An element's tag is not one the ASN.1 allows in its place.</summary>
    UnexpectedTag,

    /// <summary>A sequence ended before an element the ASN.1 requires.</summary>
    MissingElement,

    /// <summary>A sequence, or the message itself, held bytes after its last element.</summary>
    TrailingBytes,

    /// <summary>An <c>ENUMERATED</c> held a value its definition does not list.</summary>
    EnumerationOutOfRange,

    /// <summary>
    /// An element's content is not a valid value of its type: a malformed integer or boolean, an
    /// integer out of its range, a string that is not UTF-8, or a <c>SUBSTRING</c> or
    /// <c>MatchingRuleAssertion</c> that breaks its constraints.
    /// </summary>
    InvalidValue,

    /// <summary>The filter nests <c>and</c>, <c>or</c> and <c>not</c> deeper than the given bound.</summary>
    FilterTooDeep,
}
