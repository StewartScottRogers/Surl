namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// An AMQP 1.0 described value (Part 1 section 1.2): a descriptor - a <c>ulong</c> code or a
/// <c>symbol</c> name - and the value it describes. Composite types such as the performatives are
/// described lists (section 1.4).
/// </summary>
/// <param name="Descriptor">The descriptor, usually a <see cref="ulong"/> or an <see cref="Amqp1Symbol"/>.</param>
/// <param name="Value">The described value.</param>
public sealed record Amqp1Described(object? Descriptor, object? Value);
