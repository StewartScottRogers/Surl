namespace Surl.Extended.Protocol.Stomp;

/// <summary>A SUBSCRIBE's <c>ack</c> mode (the specification's "SUBSCRIBE ack Header"; ADR-0118 decision 5).</summary>
internal enum StompAckMode
{
    /// <summary><c>auto</c>, the default: a message is acknowledged when its MESSAGE is written.</summary>
    Auto,

    /// <summary><c>client</c>: an ACK or NACK settles the named message and every earlier one of its subscription.</summary>
    Client,

    /// <summary><c>client-individual</c> (1.1 and 1.2): an ACK or NACK settles the named message only.</summary>
    ClientIndividual,
}
