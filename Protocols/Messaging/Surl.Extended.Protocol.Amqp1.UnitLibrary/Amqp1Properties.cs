namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The <c>properties</c> message section (Part 3 section 3.2.4): the message's immutable
/// properties. Every field is optional and <see langword="null"/> when absent.
/// </summary>
public sealed record Amqp1Properties : Amqp1DescribedList
{
    /// <summary>The message-id, as read: a <c>ulong</c>, <c>uuid</c>, <c>binary</c> or <c>string</c>.</summary>
    public object? MessageId { get; init; }

    /// <summary>The identity of the user who produced the message.</summary>
    public byte[]? UserId { get; init; }

    /// <summary>The address of the node the message is destined for.</summary>
    public string? To { get; init; }

    /// <summary>The message's subject.</summary>
    public string? Subject { get; init; }

    /// <summary>The address to reply to.</summary>
    public string? ReplyTo { get; init; }

    /// <summary>The correlation-id, as read: a <c>ulong</c>, <c>uuid</c>, <c>binary</c> or <c>string</c>.</summary>
    public object? CorrelationId { get; init; }

    /// <summary>The MIME type of the application data.</summary>
    public Amqp1Symbol? ContentType { get; init; }

    /// <summary>The content coding applied to the application data.</summary>
    public Amqp1Symbol? ContentEncoding { get; init; }

    /// <summary>When the message expires.</summary>
    public Amqp1Timestamp? AbsoluteExpiryTime { get; init; }

    /// <summary>When the message was created.</summary>
    public Amqp1Timestamp? CreationTime { get; init; }

    /// <summary>The group the message belongs to.</summary>
    public string? GroupId { get; init; }

    /// <summary>The message's place in its group.</summary>
    public uint? GroupSequence { get; init; }

    /// <summary>The group replies belong to.</summary>
    public string? ReplyToGroupId { get; init; }

    /// <inheritdoc/>
    public override ulong DescriptorCode => Amqp1Descriptor.Properties;

    /// <summary>Reads <c>properties</c> from its described list.</summary>
    /// <param name="described">The described value.</param>
    /// <returns>The properties.</returns>
    /// <exception cref="Amqp1DecodeException">It is not a <c>properties</c>, or a field is of the wrong type.</exception>
    public static Amqp1Properties FromDescribed(Amqp1Described described)
    {
        ArgumentNullException.ThrowIfNull(described);

        var fields = Amqp1Fields.OfDescribedList(described, Amqp1Descriptor.Properties);
        return new()
        {
            MessageId = fields.Any(0),
            UserId = fields.Optional<byte[]>(1),
            To = fields.Optional<string>(2),
            Subject = fields.Optional<string>(3),
            ReplyTo = fields.Optional<string>(4),
            CorrelationId = fields.Any(5),
            ContentType = fields.OptionalValue<Amqp1Symbol>(6),
            ContentEncoding = fields.OptionalValue<Amqp1Symbol>(7),
            AbsoluteExpiryTime = fields.OptionalValue<Amqp1Timestamp>(8),
            CreationTime = fields.OptionalValue<Amqp1Timestamp>(9),
            GroupId = fields.Optional<string>(10),
            GroupSequence = fields.OptionalValue<uint>(11),
            ReplyToGroupId = fields.Optional<string>(12),
        };
    }

    /// <inheritdoc/>
    internal override IReadOnlyList<object?> FieldValues() =>
    [
        MessageId,
        UserId,
        To,
        Subject,
        ReplyTo,
        CorrelationId,
        ContentType,
        ContentEncoding,
        AbsoluteExpiryTime,
        CreationTime,
        GroupId,
        GroupSequence,
        ReplyToGroupId,
    ];
}
