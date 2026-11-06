namespace Surl.Extended.Protocol.Amqp1;

/// <summary>One token of a JMS selector.</summary>
/// <param name="Kind">What the token is.</param>
/// <param name="Value">
/// A literal's value (a <c>string</c>, <c>long</c>, <c>double</c> or <c>bool</c>) or an identifier's
/// name; <see langword="null"/> for every other kind.
/// </param>
internal readonly record struct Amqp1SelectorToken(Amqp1SelectorTokenKind Kind, object? Value = null);
