namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// An AMQP 1.0 <c>timestamp</c> (Part 1 section 1.6.20): milliseconds since the Unix epoch, kept as
/// the signed 64-bit count on the wire so that every value round-trips.
/// </summary>
/// <param name="MillisecondsSinceUnixEpoch">Milliseconds since 1970-01-01T00:00:00Z.</param>
public readonly record struct Amqp1Timestamp(long MillisecondsSinceUnixEpoch);
