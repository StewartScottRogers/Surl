namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// A decimal field value (section 4.2.5.5): <paramref name="Value"/> divided by ten to the power
/// <paramref name="Scale"/>.
/// </summary>
/// <param name="Scale">The number of decimal digits after the point.</param>
/// <param name="Value">The unscaled value.</param>
public readonly record struct Amqp0Decimal(byte Scale, uint Value);
