namespace Surl.MessageBus;

/// <summary>
/// One <see cref="BusMessage"/> as the message bus files hold it (<see cref="MessageBusFiles"/>),
/// every property kept, so a message loaded back equals the one saved.
/// </summary>
/// <remarks>
/// In order: the topic (4-byte length, UTF-8); a flags byte - <c>0x01</c> retain,
/// <c>0x02</c> UTF-8 payload, <c>0x04</c> an expiry follows, <c>0x08</c> a content type,
/// <c>0x10</c> a response topic, <c>0x20</c> correlation data, <c>0x40</c> a key, <c>0x80</c> a native
/// form (<see cref="BusMessage.NativeForm"/>); the quality of service byte (0, 1 or 2); the publish time (8 bytes, UTC ticks);
/// then each present optional field in that order - the expiry as 8 bytes of UTC ticks, the
/// others as a 4-byte length and their bytes (UTF-8 for the strings), the native form as its
/// format (4-byte length, UTF-8) and then its bytes (4-byte length and the bytes); the header count
/// (4 bytes), each header's name and value (4-byte length, UTF-8); and last the payload (4-byte
/// length and its bytes). Every integer is big-endian. A file written before native forms
/// existed never set <c>0x80</c>, so its messages load with none.
/// </remarks>
internal static class BusMessageCodec
{
    private const byte RetainFlag = 0x01;
    private const byte Utf8PayloadFlag = 0x02;
    private const byte ExpiresAtFlag = 0x04;
    private const byte ContentTypeFlag = 0x08;
    private const byte ResponseTopicFlag = 0x10;
    private const byte CorrelationDataFlag = 0x20;
    private const byte KeyFlag = 0x40;
    private const byte NativeFormFlag = 0x80;

    public static void Write(BusFileWriter writer, BusMessage message)
    {
        writer.WriteString(message.Topic);
        writer.WriteByte(Flags(message));
        writer.WriteByte((byte)message.QualityOfService);
        writer.WriteTime(message.PublishedAt);
        WriteOptionalFields(writer, message);
        writer.WriteCount(message.Headers.Count);
        foreach (var header in message.Headers)
        {
            writer.WriteString(header.Name);
            writer.WriteString(header.Value);
        }

        writer.WriteBytes(message.Payload.Span);
    }

    /// <summary>Reads one message.</summary>
    /// <exception cref="InvalidDataException">It does not parse.</exception>
    public static BusMessage Read(BusFileReader reader)
    {
        var topic = reader.ReadString();
        var flags = reader.ReadByte();
        var qualityOfService = reader.ReadByte();
        if (qualityOfService > (byte)BusQualityOfService.ExactlyOnce)
        {
            throw reader.Malformed();
        }

        var message = new BusMessage(topic, ReadOnlyMemory<byte>.Empty)
        {
            Retain = Has(flags, RetainFlag),
            Utf8Payload = Has(flags, Utf8PayloadFlag),
            QualityOfService = (BusQualityOfService)qualityOfService,
            PublishedAt = reader.ReadTime(),
        };
        message = ReadOptionalFields(reader, flags, message);
        var headers = new BusMessageHeader[reader.ReadCount()];
        for (var index = 0; index < headers.Length; index++)
        {
            headers[index] = new(reader.ReadString(), reader.ReadString());
        }

        return message with { Headers = headers, Payload = reader.ReadBytes() };
    }

    private static byte Flags(BusMessage message) => (byte)(
        (message.Retain ? RetainFlag : 0)
        | (message.Utf8Payload ? Utf8PayloadFlag : 0)
        | (message.ExpiresAt is null ? 0 : ExpiresAtFlag)
        | (message.ContentType is null ? 0 : ContentTypeFlag)
        | (message.ResponseTopic is null ? 0 : ResponseTopicFlag)
        | OptionalByteFieldFlags(message));

    private static int OptionalByteFieldFlags(BusMessage message) =>
        (message.CorrelationData is null ? 0 : CorrelationDataFlag)
        | (message.Key is null ? 0 : KeyFlag)
        | (message.NativeForm is null ? 0 : NativeFormFlag);

    private static void WriteOptionalFields(BusFileWriter writer, BusMessage message)
    {
        WriteOptionalTextFields(writer, message);
        WriteOptionalByteFields(writer, message);
    }

    private static void WriteOptionalTextFields(BusFileWriter writer, BusMessage message)
    {
        if (message.ExpiresAt is { } expiresAt)
        {
            writer.WriteTime(expiresAt);
        }

        if (message.ContentType is { } contentType)
        {
            writer.WriteString(contentType);
        }

        if (message.ResponseTopic is { } responseTopic)
        {
            writer.WriteString(responseTopic);
        }
    }

    private static void WriteOptionalByteFields(BusFileWriter writer, BusMessage message)
    {
        if (message.CorrelationData is { } correlationData)
        {
            writer.WriteBytes(correlationData.Span);
        }

        if (message.Key is { } key)
        {
            writer.WriteBytes(key.Span);
        }

        if (message.NativeForm is { } nativeForm)
        {
            writer.WriteString(nativeForm.Format);
            writer.WriteBytes(nativeForm.Bytes.Span);
        }
    }

    private static BusMessage ReadOptionalFields(BusFileReader reader, byte flags, BusMessage message) => ReadOptionalByteFields(
        reader,
        flags,
        message with
        {
            ExpiresAt = Has(flags, ExpiresAtFlag) ? reader.ReadTime() : null,
            ContentType = Has(flags, ContentTypeFlag) ? reader.ReadString() : null,
            ResponseTopic = Has(flags, ResponseTopicFlag) ? reader.ReadString() : null,
        });

    private static BusMessage ReadOptionalByteFields(BusFileReader reader, byte flags, BusMessage message) => message with
    {
        CorrelationData = Has(flags, CorrelationDataFlag) ? (ReadOnlyMemory<byte>?)reader.ReadBytes() : null,
        Key = Has(flags, KeyFlag) ? (ReadOnlyMemory<byte>?)reader.ReadBytes() : null,
        NativeForm = Has(flags, NativeFormFlag) ? new BusNativeForm(reader.ReadString(), reader.ReadBytes()) : null,
    };

    private static bool Has(byte flags, byte flag) => (flags & flag) != 0;
}
