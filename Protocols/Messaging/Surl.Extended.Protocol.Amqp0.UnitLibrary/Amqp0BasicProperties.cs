namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The <c>basic</c> class's content properties (the XML's <c>basic</c> fields; section 4.2.6.1):
/// each present property's flag is set, bit 15 for <see cref="ContentType"/> down to bit 2 for
/// <see cref="ClusterId"/>, and the present ones follow in that order.
/// </summary>
public sealed record Amqp0BasicProperties
{
    private const int ContentTypeBit = 15;
    private const int ContentEncodingBit = 14;
    private const int HeadersBit = 13;
    private const int DeliveryModeBit = 12;
    private const int PriorityBit = 11;
    private const int CorrelationIdBit = 10;
    private const int ReplyToBit = 9;
    private const int ExpirationBit = 8;
    private const int MessageIdBit = 7;
    private const int TimestampBit = 6;
    private const int TypeBit = 5;
    private const int UserIdBit = 4;
    private const int AppIdBit = 3;
    private const int ClusterIdBit = 2;

    /// <summary><c>content-type</c>, a MIME type.</summary>
    public string? ContentType { get; init; }

    /// <summary><c>content-encoding</c>, a MIME content encoding.</summary>
    public string? ContentEncoding { get; init; }

    /// <summary><c>headers</c>, the application's header table.</summary>
    public Amqp0FieldTable? Headers { get; init; }

    /// <summary><c>delivery-mode</c>: 1 transient, 2 persistent.</summary>
    public byte? DeliveryMode { get; init; }

    /// <summary><c>priority</c>, 0 to 9.</summary>
    public byte? Priority { get; init; }

    /// <summary><c>correlation-id</c>.</summary>
    public string? CorrelationId { get; init; }

    /// <summary><c>reply-to</c>.</summary>
    public string? ReplyTo { get; init; }

    /// <summary><c>expiration</c>, a decimal count of milliseconds as text.</summary>
    public string? Expiration { get; init; }

    /// <summary><c>message-id</c>.</summary>
    public string? MessageId { get; init; }

    /// <summary><c>timestamp</c>, in seconds since the Unix epoch.</summary>
    public ulong? Timestamp { get; init; }

    /// <summary><c>type</c>, the message's type name.</summary>
    public string? Type { get; init; }

    /// <summary><c>user-id</c>.</summary>
    public string? UserId { get; init; }

    /// <summary><c>app-id</c>.</summary>
    public string? AppId { get; init; }

    /// <summary>The reserved <c>cluster-id</c> property, kept as sent.</summary>
    public string? ClusterId { get; init; }

    /// <summary>Reads the properties <paramref name="propertyFlags"/> announces from <paramref name="propertyList"/>.</summary>
    /// <param name="propertyFlags">The content header's property flags.</param>
    /// <param name="propertyList">The content header's property list.</param>
    /// <returns>The properties.</returns>
    /// <exception cref="Amqp0SyntaxException">The list does not hold what the flags announce.</exception>
    public static Amqp0BasicProperties Read(ushort propertyFlags, ReadOnlyMemory<byte> propertyList)
    {
        var reader = new PropertyReader(propertyFlags, new Amqp0WireReader(propertyList));
        return new()
        {
            ContentType = reader.ShortString(ContentTypeBit),
            ContentEncoding = reader.ShortString(ContentEncodingBit),
            Headers = reader.Table(HeadersBit),
            DeliveryMode = reader.Octet(DeliveryModeBit),
            Priority = reader.Octet(PriorityBit),
            CorrelationId = reader.ShortString(CorrelationIdBit),
            ReplyTo = reader.ShortString(ReplyToBit),
            Expiration = reader.ShortString(ExpirationBit),
            MessageId = reader.ShortString(MessageIdBit),
            Timestamp = reader.LongLong(TimestampBit),
            Type = reader.ShortString(TypeBit),
            UserId = reader.ShortString(UserIdBit),
            AppId = reader.ShortString(AppIdBit),
            ClusterId = reader.ShortString(ClusterIdBit),
        };
    }

    /// <summary>The property flags and list that carry these properties.</summary>
    /// <returns>The flags, and the present properties in order.</returns>
    public (ushort PropertyFlags, byte[] PropertyList) Encode()
    {
        var writer = new PropertyWriter();
        writer.ShortString(ContentTypeBit, ContentType);
        writer.ShortString(ContentEncodingBit, ContentEncoding);
        writer.Table(HeadersBit, Headers);
        writer.Octet(DeliveryModeBit, DeliveryMode);
        writer.Octet(PriorityBit, Priority);
        writer.ShortString(CorrelationIdBit, CorrelationId);
        writer.ShortString(ReplyToBit, ReplyTo);
        writer.ShortString(ExpirationBit, Expiration);
        writer.ShortString(MessageIdBit, MessageId);
        writer.LongLong(TimestampBit, Timestamp);
        writer.ShortString(TypeBit, Type);
        writer.ShortString(UserIdBit, UserId);
        writer.ShortString(AppIdBit, AppId);
        writer.ShortString(ClusterIdBit, ClusterId);
        return (writer.Flags, writer.Wire.ToArray());
    }

    private sealed class PropertyReader(ushort flags, Amqp0WireReader wire)
    {
        public string? ShortString(int bit) => IsSet(bit) ? wire.ReadShortString() : null;

        public Amqp0FieldTable? Table(int bit) => IsSet(bit) ? wire.ReadFieldTable() : null;

        public byte? Octet(int bit) => IsSet(bit) ? wire.ReadOctet() : null;

        public ulong? LongLong(int bit) => IsSet(bit) ? wire.ReadLongLong() : null;

        private bool IsSet(int bit) => (flags & (1 << bit)) != 0;
    }

    private sealed class PropertyWriter
    {
        public ushort Flags { get; private set; }

        public Amqp0WireWriter Wire { get; } = new();

        public void ShortString(int bit, string? value)
        {
            if (value is not null)
            {
                Mark(bit);
                Wire.WriteShortString(value);
            }
        }

        public void Table(int bit, Amqp0FieldTable? value)
        {
            if (value is not null)
            {
                Mark(bit);
                Wire.WriteFieldTable(value);
            }
        }

        public void Octet(int bit, byte? value)
        {
            if (value is { } present)
            {
                Mark(bit);
                Wire.WriteOctet(present);
            }
        }

        public void LongLong(int bit, ulong? value)
        {
            if (value is { } present)
            {
                Mark(bit);
                Wire.WriteLongLong(present);
            }
        }

        private void Mark(int bit) => Flags |= (ushort)(1 << bit);
    }
}
