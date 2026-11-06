namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>A message a selector reads in tests: a header, properties and application properties, as given.</summary>
internal sealed class UnitTestAmqp1SelectorMessage : IAmqp1SelectorMessage
{
    public Amqp1Header? Header { get; init; }

    public Amqp1Properties? Properties { get; init; }

    public Dictionary<string, object?> ApplicationProperties { get; init; } = new(StringComparer.Ordinal);

    public object? ApplicationProperty(string name) => ApplicationProperties.GetValueOrDefault(name);
}
