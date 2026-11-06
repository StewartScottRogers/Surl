namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The protocol id octet of an AMQP 1.0 protocol header (Part 2 section 2.2; Part 5 sections 5.2.1
/// and 5.3.1): which layer the bytes after the header belong to.
/// </summary>
public enum Amqp1ProtocolId : byte
{
    /// <summary><c>AMQP 0 1 0 0</c>: AMQP frames follow.</summary>
    Amqp = 0,

    /// <summary><c>AMQP 2 1 0 0</c>: a TLS handshake follows.</summary>
    Tls = 2,

    /// <summary><c>AMQP 3 1 0 0</c>: SASL frames follow.</summary>
    Sasl = 3,
}
