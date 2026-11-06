namespace Surl.Extended.Protocol.Amqp1;

/// <summary>An AMQP 1.0 <c>decimal64</c> (Part 1 section 1.6.17), kept as its IEEE 754 decimal64 bits.</summary>
/// <param name="Bits">The eight octets, big-endian.</param>
public readonly record struct Amqp1Decimal64(ulong Bits);
