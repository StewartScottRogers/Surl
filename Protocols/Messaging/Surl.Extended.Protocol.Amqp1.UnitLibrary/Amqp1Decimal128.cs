namespace Surl.Extended.Protocol.Amqp1;

/// <summary>An AMQP 1.0 <c>decimal128</c> (Part 1 section 1.6.18), kept as its IEEE 754 decimal128 bits.</summary>
/// <param name="Bits">The sixteen octets, big-endian.</param>
public readonly record struct Amqp1Decimal128(UInt128 Bits);
