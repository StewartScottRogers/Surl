namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// An AMQP 1.0 <c>symbol</c> (Part 1 section 1.6.24): a short ASCII name such as
/// <c>amqp:open:list</c> or <c>PLAIN</c>, kept apart from a <c>string</c> because the two encode
/// with different format codes.
/// </summary>
/// <param name="Value">The ASCII text.</param>
public readonly record struct Amqp1Symbol(string Value);
