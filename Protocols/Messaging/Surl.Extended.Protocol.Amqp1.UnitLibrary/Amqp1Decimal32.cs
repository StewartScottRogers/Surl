namespace Surl.Extended.Protocol.Amqp1;

/// <summary>An AMQP 1.0 <c>decimal32</c> (Part 1 section 1.6.16), kept as its IEEE 754 decimal32 bits.</summary>
/// <param name="Bits">The four octets, big-endian.</param>
public readonly record struct Amqp1Decimal32(uint Bits);
