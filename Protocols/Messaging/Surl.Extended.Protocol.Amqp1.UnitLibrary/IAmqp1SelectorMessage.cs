namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The parts of a message a JMS selector reads (ADR-0146 decision 2.2): its <c>header</c>, its
/// <c>properties</c> and its application properties, so a selector is evaluated against a decoded
/// AMQP 1.0 message or a message another protocol published alike.
/// </summary>
public interface IAmqp1SelectorMessage
{
    /// <summary>The message's <c>header</c> section; <see langword="null"/> when it has none.</summary>
    Amqp1Header? Header { get; }

    /// <summary>The message's <c>properties</c> section; <see langword="null"/> when it has none.</summary>
    Amqp1Properties? Properties { get; }

    /// <summary>The value of the application property of that name, as decoded.</summary>
    /// <param name="name">The property's name, matched exactly.</param>
    /// <returns>The value; <see langword="null"/> when the message has no such property.</returns>
    object? ApplicationProperty(string name);
}
