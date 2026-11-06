using System.Buffers.Binary;
using System.Text;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Turns a message a client sent into a <see cref="BusMessage"/>, and a bus message into the bytes a
/// transfer to a client receiver carries (ADR-0107 decision 6; ADR-0100 decision 3.4).
/// </summary>
internal static class Amqp1BusMessages
{
    /// <summary>The native form's format: <c>amqp1</c> (BL-706).</summary>
    public const string NativeFormat = "amqp1";

    // An absolute-expiry-time beyond what a DateTimeOffset holds sets no expiry.
    private const long EarliestUnixMilliseconds = -62_135_596_800_000; // DateTimeOffset.MinValue

    private const long LatestUnixMilliseconds = 253_402_300_799_999; // DateTimeOffset.MaxValue

    private static readonly HashSet<ulong> MapSections =
        [Amqp1Descriptor.DeliveryAnnotations, Amqp1Descriptor.MessageAnnotations, Amqp1Descriptor.ApplicationProperties, Amqp1Descriptor.Footer];

    /// <summary>
    /// The bus message for a message a client sent: its sections onto the bus's fields and, as the native
    /// form, every section but the delivery annotations byte for byte.
    /// </summary>
    /// <param name="message">The message: the joined payloads of a delivery's transfers.</param>
    /// <param name="topic">The bus topic, or for a queue its name.</param>
    /// <param name="presettled">Whether the client sent the delivery settled: QoS 0, else QoS 1.</param>
    /// <param name="now">The time of the publish, from which a <c>ttl</c> runs.</param>
    /// <returns>The bus message.</returns>
    /// <exception cref="Amqp1DecodeException">The bytes are not a message of Part 3 section 3.2.</exception>
    public static BusMessage ToBusMessage(ReadOnlyMemory<byte> message, string topic, bool presettled, DateTimeOffset now)
    {
        var sections = Amqp1MessageSection.ReadAll(message);
        RequireSectionOrder(sections);
        var header = SectionOf(sections, Amqp1Descriptor.Header) is { } headerSection ? Amqp1Header.FromDescribed(headerSection.Described) : null;
        var properties = SectionOf(sections, Amqp1Descriptor.Properties) is { } propertiesSection ? Amqp1Properties.FromDescribed(propertiesSection.Described) : null;
        var (payload, utf8) = PayloadOf(sections);
        return new(topic, payload)
        {
            QualityOfService = presettled ? BusQualityOfService.AtMostOnce : BusQualityOfService.AtLeastOnce,
            ExpiresAt = ExpiresAt(header?.Ttl, properties?.AbsoluteExpiryTime, now),
            ContentType = properties?.ContentType?.Value,
            ResponseTopic = ResponseTopicOf(properties?.ReplyTo),
            CorrelationData = CorrelationOf(properties?.CorrelationId),
            Headers = HeadersOf(SectionOf(sections, Amqp1Descriptor.ApplicationProperties)),
            Utf8Payload = utf8,
            NativeForm = new(NativeFormat, NativeBytes(sections)),
        };
    }

    // Part 3 section 3.2: the sections in their order, each at most once, but data and amqp-sequence
    // sections, which repeat; one kind of body; and each map section a map, each data section binary.
    private static void RequireSectionOrder(IReadOnlyList<Amqp1MessageSection> sections)
    {
        for (var index = 1; index < sections.Count; index++)
        {
            var (previous, current) = (sections[index - 1].DescriptorCode, sections[index].DescriptorCode);
            if (current < previous || (current == previous && !IsRepeatableBody(current)))
            {
                throw new Amqp1DecodeException("The sections of a message are out of order or repeated.");
            }
        }

        if (sections.Any(section => !HoldsItsType(section)) || sections.Where(IsBody).DistinctBy(section => section.DescriptorCode).Count() > 1)
        {
            throw new Amqp1DecodeException("A message section holds a value of the wrong type.");
        }
    }

    private static bool IsRepeatableBody(ulong code) => code is Amqp1Descriptor.Data or Amqp1Descriptor.AmqpSequence;

    private static bool HoldsItsType(Amqp1MessageSection section)
    {
        var (code, value) = (section.DescriptorCode, section.Described.Value);
        if (MapSections.Contains(code))
        {
            return value is Amqp1Map;
        }

        if (code == Amqp1Descriptor.Data)
        {
            return value is byte[];
        }

        return code != Amqp1Descriptor.AmqpSequence || value is IReadOnlyList<object?>;
    }

    private static Amqp1MessageSection? SectionOf(IReadOnlyList<Amqp1MessageSection> sections, ulong code) =>
        sections.FirstOrDefault(section => section.DescriptorCode == code);

    private static bool IsBody(Amqp1MessageSection section) => section.DescriptorCode is >= Amqp1Descriptor.Data and <= Amqp1Descriptor.AmqpValue;

    // The data sections' bytes joined; an amqp-value binary's bytes or string's UTF-8; or else the body
    // sections' own encoding.
    private static (byte[] Payload, bool Utf8) PayloadOf(IReadOnlyList<Amqp1MessageSection> sections)
    {
        var body = sections.Where(IsBody).ToList();
        if (body.Count == 0 || body[0].DescriptorCode == Amqp1Descriptor.Data)
        {
            return ([.. body.SelectMany(section => (byte[])section.Described.Value!)], false);
        }

        return body[0].Described.Value switch
        {
            byte[] bytes => (bytes, false),
            string text => (Encoding.UTF8.GetBytes(text), true),
            _ => ([.. body.SelectMany(section => section.Encoded.ToArray())], false),
        };
    }

    private static DateTimeOffset? ExpiresAt(uint? ttl, Amqp1Timestamp? absoluteExpiryTime, DateTimeOffset now)
    {
        DateTimeOffset? byTtl = ttl is { } milliseconds ? now.AddMilliseconds(milliseconds) : null;
        DateTimeOffset? byTime = absoluteExpiryTime is { MillisecondsSinceUnixEpoch: >= EarliestUnixMilliseconds and <= LatestUnixMilliseconds } time
            ? DateTimeOffset.FromUnixTimeMilliseconds(time.MillisecondsSinceUnixEpoch)
            : null;
        if (byTtl is not { } ttlAt)
        {
            return byTime;
        }

        return byTime is { } timeAt && timeAt < ttlAt ? timeAt : ttlAt;
    }

    // A reply-to naming a topic by ADR-0107 decision 4's rules is the canonical topic; a queue's has
    // no bus field and travels in the native form only.
    private static string? ResponseTopicOf(string? replyTo)
    {
        var canonical = Amqp1NodeAddress.Canonical(replyTo, forSending: true);
        return canonical is not null && Amqp1NodeAddress.IsTopic(canonical) && Amqp1AddressNames.ToPublishDestination(canonical).Destination is { } destination
            ? destination.Name
            : null;
    }

    // A bare null here would convert through byte[] into an empty memory rather than none.
    private static ReadOnlyMemory<byte>? CorrelationOf(object? correlationId) => CorrelationDataOf(correlationId) is { } bytes ? new ReadOnlyMemory<byte>(bytes) : (ReadOnlyMemory<byte>?)null;

    private static byte[]? CorrelationDataOf(object? correlationId) => correlationId switch
    {
        byte[] bytes => bytes,
        string text => Encoding.UTF8.GetBytes(text),
        Guid uuid => uuid.ToByteArray(bigEndian: true),
        ulong number => BigEndian(number),
        _ => null,
    };

    private static byte[] BigEndian(ulong number)
    {
        var bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, number);
        return bytes;
    }

    private static IReadOnlyList<BusMessageHeader> HeadersOf(Amqp1MessageSection? applicationProperties) =>
        applicationProperties is null
            ? []
            : [.. ((Amqp1Map)applicationProperties.Described.Value!).Entries
                .Where(entry => entry is { Key: string, Value: string })
                .Select(entry => new BusMessageHeader((string)entry.Key!, (string)entry.Value!))];

    // Part 3 section 3.2.2: delivery annotations are for the next hop only, so they are not kept.
    private static byte[] NativeBytes(IReadOnlyList<Amqp1MessageSection> sections) =>
        [.. sections.Where(section => section.DescriptorCode != Amqp1Descriptor.DeliveryAnnotations).SelectMany(section => section.Encoded.ToArray())];

    /// <summary>
    /// What a transfer to a client receiver carries for <paramref name="message"/>: an <c>amqp1</c>
    /// native form's sections unchanged, its header rebuilt for a redelivery; any other message as a
    /// header when it has a <c>ttl</c> or a redelivery to carry, properties, application properties and
    /// one <c>data</c> section.
    /// </summary>
    /// <param name="message">The bus message.</param>
    /// <param name="address">The full address <c>to</c> carries: <c>/topic/&lt;t&gt;</c> or <c>/queue/&lt;q&gt;</c>.</param>
    /// <param name="redelivered">Whether the bus marks the delivery redelivered.</param>
    /// <param name="now">The time of sending, from which a remaining <c>ttl</c> is counted.</param>
    /// <returns>The message bytes.</returns>
    public static byte[] ToTransferPayload(BusMessage message, string address, bool redelivered, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.NativeForm is { Format: NativeFormat } native
            ? NativePayload(native.Bytes, redelivered)
            : BuiltPayload(message, address, redelivered, now);
    }

    private static byte[] NativePayload(ReadOnlyMemory<byte> native, bool redelivered)
    {
        if (!redelivered)
        {
            return native.ToArray();
        }

        var sections = Amqp1MessageSection.ReadAll(native);
        var header = SectionOf(sections, Amqp1Descriptor.Header) is { } headerSection ? Amqp1Header.FromDescribed(headerSection.Described) : new Amqp1Header();
        var redeliveredHeader = header with { DeliveryCount = header.DeliveryCount + 1, FirstAcquirer = false };
        return [.. Amqp1Encoder.Encode(redeliveredHeader.ToDescribed()), .. sections.Where(section => section.DescriptorCode != Amqp1Descriptor.Header).SelectMany(section => section.Encoded.ToArray())];
    }

    private static byte[] BuiltPayload(BusMessage message, string address, bool redelivered, DateTimeOffset now)
    {
        var encoder = new Amqp1Encoder();
        if (HeaderOf(message, redelivered, now) is { } header)
        {
            encoder.WriteValue(header.ToDescribed());
        }

        encoder.WriteValue(PropertiesOf(message, address).ToDescribed());
        if (message.Headers.Count > 0)
        {
            encoder.WriteValue(new Amqp1Described(Amqp1Descriptor.ApplicationProperties, ApplicationPropertiesOf(message.Headers)));
        }

        encoder.WriteValue(new Amqp1Described(Amqp1Descriptor.Data, message.Payload.ToArray()));
        return encoder.Written.ToArray();
    }

    private static Amqp1Header? HeaderOf(BusMessage message, bool redelivered, DateTimeOffset now)
    {
        if (message.ExpiresAt is null && !redelivered)
        {
            return null;
        }

        return new Amqp1Header
        {
            Ttl = message.ExpiresAt is { } expiresAt ? RemainingMilliseconds(expiresAt - now) : null,
            DeliveryCount = redelivered ? 1u : 0u,
        };
    }

    private static uint RemainingMilliseconds(TimeSpan remaining) => (uint)Math.Clamp(remaining.TotalMilliseconds, 0, uint.MaxValue);

    private static Amqp1Properties PropertiesOf(BusMessage message, string address) => new()
    {
        To = address,
        CreationTime = new Amqp1Timestamp(message.PublishedAt.ToUnixTimeMilliseconds()),
        ContentType = message.ContentType is { } contentType ? new Amqp1Symbol(contentType) : null,
        ReplyTo = message.ResponseTopic is { } responseTopic ? Amqp1NodeAddress.TopicPrefix + responseTopic : null,
        CorrelationId = message.CorrelationData?.ToArray(),
    };

    // An AMQP map has unique keys, so the first of a repeated name is kept.
    private static Amqp1Map ApplicationPropertiesOf(IReadOnlyList<BusMessageHeader> headers) =>
        new([.. headers.DistinctBy(header => header.Name, StringComparer.Ordinal).Select(header => new KeyValuePair<object?, object?>(header.Name, header.Value))]);
}
