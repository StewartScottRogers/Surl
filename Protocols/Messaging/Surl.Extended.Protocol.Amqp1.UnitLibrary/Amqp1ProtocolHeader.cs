namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The 8-byte AMQP 1.0 protocol header (Part 2 section 2.2): <c>A M Q P</c>, a protocol id, then
/// major 1, minor 0, revision 0.
/// </summary>
public static class Amqp1ProtocolHeader
{
    /// <summary>The bytes of a protocol header.</summary>
    public const int Bytes = 8;

    /// <summary>The header announcing <paramref name="protocolId"/> at version 1.0.0.</summary>
    /// <param name="protocolId">The layer that follows.</param>
    /// <returns>The 8 bytes.</returns>
    public static byte[] Of(Amqp1ProtocolId protocolId) => [(byte)'A', (byte)'M', (byte)'Q', (byte)'P', (byte)protocolId, 1, 0, 0];

    /// <summary>
    /// The protocol id of <paramref name="header"/> when it is an AMQP 1.0.0 header for the AMQP,
    /// TLS or SASL layer; <see langword="null"/> for any other 8 bytes - another version, another
    /// protocol id, AMQP 0-9-1's <c>AMQP 0 0 9 1</c>, or not <c>AMQP</c> at all (ADR-0107 decision 1).
    /// </summary>
    /// <param name="header">The 8 bytes the client sent.</param>
    /// <returns>The protocol id, or <see langword="null"/> when the header is not one the container accepts.</returns>
    public static Amqp1ProtocolId? Identify(ReadOnlySpan<byte> header)
    {
        var protocolId = (Amqp1ProtocolId)header[4];
        return Enum.IsDefined(protocolId) && header.SequenceEqual(Of(protocolId)) ? protocolId : null;
    }
}
