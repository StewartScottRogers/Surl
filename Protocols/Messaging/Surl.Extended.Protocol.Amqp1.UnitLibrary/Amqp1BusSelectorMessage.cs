using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// A bus message as a JMS selector and a source filter read it (ADR-0146 decisions 2.1 and 2.2): the
/// <c>header</c>, <c>properties</c> and application properties of the message a client receiver would
/// be sent (<see cref="Amqp1BusMessages.ToTransferPayload"/>), so a message another protocol published
/// is read through the fields ADR-0107 decision 6 maps it to.
/// </summary>
internal sealed class Amqp1BusSelectorMessage : IAmqp1SelectorMessage
{
    private static readonly Amqp1BusSelectorMessage Empty = new(null, null, null);

    private readonly Amqp1Map? applicationProperties;

    private Amqp1BusSelectorMessage(Amqp1Header? header, Amqp1Properties? properties, Amqp1Map? applicationProperties)
    {
        Header = header;
        Properties = properties;
        this.applicationProperties = applicationProperties;
    }

    /// <inheritdoc/>
    public Amqp1Header? Header { get; }

    /// <inheritdoc/>
    public Amqp1Properties? Properties { get; }

    /// <summary>
    /// Reads a bus message's sections. A native form that does not decode reads as a message with no
    /// header, properties or application properties, so every filter but an empty selector refuses it.
    /// </summary>
    /// <param name="message">The bus message.</param>
    /// <returns>The message's selector view.</returns>
    public static Amqp1BusSelectorMessage Read(BusMessage message)
    {
        try
        {
            var sections = Amqp1MessageSection.ReadAll(Amqp1BusMessages.ToTransferPayload(message, string.Empty, redelivered: false, message.PublishedAt));
            return new(
                SectionOf(sections, Amqp1Descriptor.Header) is { } header ? Amqp1Header.FromDescribed(header) : null,
                SectionOf(sections, Amqp1Descriptor.Properties) is { } properties ? Amqp1Properties.FromDescribed(properties) : null,
                SectionOf(sections, Amqp1Descriptor.ApplicationProperties)?.Value as Amqp1Map);
        }
        catch (Amqp1DecodeException)
        {
            return Empty;
        }
    }

    private static Amqp1Described? SectionOf(IReadOnlyList<Amqp1MessageSection> sections, ulong code) =>
        sections.FirstOrDefault(section => section.DescriptorCode == code)?.Described;

    /// <inheritdoc/>
    public object? ApplicationProperty(string name) =>
        applicationProperties?.Entries.FirstOrDefault(entry => entry.Key is string key && key == name).Value;
}
