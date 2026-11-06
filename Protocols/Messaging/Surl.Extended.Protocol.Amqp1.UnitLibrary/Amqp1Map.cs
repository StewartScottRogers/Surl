namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// An AMQP 1.0 <c>map</c> (Part 1 section 1.6.23): key-value pairs in the order they were read or
/// are to be written. Keys are not checked for uniqueness: the map is kept as it travels.
/// </summary>
/// <param name="Entries">The pairs, in order.</param>
public sealed record Amqp1Map(IReadOnlyList<KeyValuePair<object?, object?>> Entries);
