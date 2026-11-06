namespace Surl.Extended.Protocol.Amqp0;

/// <summary>One field of a field table: its name, a short string, and its value.</summary>
/// <param name="Name">The field's name.</param>
/// <param name="Value">The field's value.</param>
public sealed record Amqp0Field(string Name, Amqp0FieldValue Value);
