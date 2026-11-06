using System.Text;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// A client's message onto the bus and a bus message into a transfer (AMQP 1.0 Part 3 section 3.2;
/// ADR-0107 decision 6). Sections are built with the library's codec, which BL-510's tests pin.
/// </summary>
[TestClass]
public sealed class Amqp1BusMessagesTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeMilliseconds(1_000_000);

    [TestMethod]
    public void ToBusMessage_PropertiesAndHeader_FillTheBusFields()
    {
        var message = Join(
            Section(Amqp1Descriptor.Header, new Amqp1Header { Ttl = 5000 }.ToDescribed().Value),
            Section(Amqp1Descriptor.Properties, new Amqp1Properties
            {
                ReplyTo = "/topic/replies",
                ContentType = new Amqp1Symbol("text/plain"),
                CorrelationId = "c1",
                AbsoluteExpiryTime = new Amqp1Timestamp(1_010_000),
            }.ToDescribed().Value),
            Section(Amqp1Descriptor.ApplicationProperties, new Amqp1Map([Entry("a", "1"), Entry("n", 2), Entry(new Amqp1Symbol("s"), "x")])),
            Section(Amqp1Descriptor.Data, "ab"u8.ToArray()),
            Section(Amqp1Descriptor.Data, "cd"u8.ToArray()),
            Section(Amqp1Descriptor.Footer, new Amqp1Map([])));

        var bus = Amqp1BusMessages.ToBusMessage(message, "news", presettled: true, Now);

        Assert.AreEqual("abcd", Encoding.UTF8.GetString(bus.Payload.Span));
        Assert.AreEqual(BusQualityOfService.AtMostOnce, bus.QualityOfService);
        Assert.AreEqual(Now.AddMilliseconds(5000), bus.ExpiresAt);
        Assert.AreEqual("text/plain", bus.ContentType);
        Assert.AreEqual("replies", bus.ResponseTopic);
        Assert.AreEqual("c1", Encoding.UTF8.GetString(bus.CorrelationData!.Value.Span));
        CollectionAssert.AreEqual(new[] { new BusMessageHeader("a", "1") }, bus.Headers.ToArray());
        Assert.IsFalse(bus.Utf8Payload);
        CollectionAssert.AreEqual(message, bus.NativeForm!.Bytes.ToArray());
        Assert.AreEqual(Amqp1BusMessages.NativeFormat, bus.NativeForm.Format);
    }

    [TestMethod]
    [DataRow(null, 2000L, 2000L, DisplayName = "only an absolute expiry time")]
    [DataRow(5000u, 2000L, 2000L, DisplayName = "an absolute expiry time before the ttl")]
    [DataRow(null, null, null, DisplayName = "neither")]
    [DataRow(3000u, null, 3000L, DisplayName = "only a ttl")]
    public void ToBusMessage_ExpiryTimes_TheEarlierIsKept(uint? ttl, long? absoluteMilliseconds, long? expectedOffsetMilliseconds)
    {
        var message = Join(
            Section(Amqp1Descriptor.Header, new Amqp1Header { Ttl = ttl }.ToDescribed().Value),
            Section(Amqp1Descriptor.Properties, new Amqp1Properties
            {
                AbsoluteExpiryTime = absoluteMilliseconds is { } milliseconds ? new Amqp1Timestamp(Now.ToUnixTimeMilliseconds() + milliseconds) : null,
            }.ToDescribed().Value));

        var bus = Amqp1BusMessages.ToBusMessage(message, "news", presettled: false, Now);

        Assert.AreEqual(expectedOffsetMilliseconds is { } offset ? Now.AddMilliseconds(offset) : null, bus.ExpiresAt);
        Assert.IsTrue(bus.Payload.IsEmpty);
    }

    [TestMethod]
    [DataRow(253_402_300_799_999L, true, DisplayName = "the latest a DateTimeOffset holds")]
    [DataRow(253_402_300_800_000L, false, DisplayName = "past the latest")]
    [DataRow(-62_135_596_800_000L, true, DisplayName = "the earliest a DateTimeOffset holds")]
    [DataRow(-62_135_596_800_001L, false, DisplayName = "before the earliest")]
    public void ToBusMessage_AbsoluteExpiryTimeBeyondADateTimeOffset_SetsNoExpiry(long milliseconds, bool expires)
    {
        var message = Section(Amqp1Descriptor.Properties, new Amqp1Properties { AbsoluteExpiryTime = new Amqp1Timestamp(milliseconds) }.ToDescribed().Value);

        var bus = Amqp1BusMessages.ToBusMessage(message, "news", presettled: false, Now);

        Assert.AreEqual(expires, bus.ExpiresAt is not null);
    }

    [TestMethod]
    [DataRow("/queue/replies", DisplayName = "a queue")]
    [DataRow("/topic/", DisplayName = "no topic")]
    [DataRow("/topic/a/#", DisplayName = "a filter")]
    public void ToBusMessage_ReplyToNamingNoTopic_HasNoResponseTopic(string replyTo)
    {
        var message = Section(Amqp1Descriptor.Properties, new Amqp1Properties { ReplyTo = replyTo }.ToDescribed().Value);

        Assert.IsNull(Amqp1BusMessages.ToBusMessage(message, "news", presettled: false, Now).ResponseTopic);
    }

    [TestMethod]
    public void ToBusMessage_CorrelationIds_AreTheirBytes()
    {
        var uuid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        Assert.AreEqual("0102", CorrelationHex(new byte[] { 1, 2 }));
        Assert.AreEqual("00112233445566778899AABBCCDDEEFF", CorrelationHex(uuid));
        Assert.AreEqual("0000000000000102", CorrelationHex(258ul));
        Assert.IsFalse(Amqp1BusMessages.ToBusMessage(CorrelatedBy(new Amqp1Symbol("s")), "news", false, Now).CorrelationData.HasValue);
    }

    [TestMethod]
    public void ToBusMessage_AmqpValueString_IsItsUtf8()
    {
        var bus = Amqp1BusMessages.ToBusMessage(Section(Amqp1Descriptor.AmqpValue, "héllo"), "news", false, Now);

        Assert.AreEqual("héllo", Encoding.UTF8.GetString(bus.Payload.Span));
        Assert.IsTrue(bus.Utf8Payload);
    }

    [TestMethod]
    public void ToBusMessage_AmqpValueBinary_IsItsBytes()
    {
        var bus = Amqp1BusMessages.ToBusMessage(Section(Amqp1Descriptor.AmqpValue, new byte[] { 9 }), "news", false, Now);

        CollectionAssert.AreEqual(new byte[] { 9 }, bus.Payload.ToArray());
    }

    [TestMethod]
    public void ToBusMessage_OtherAmqpValueOrSequences_AreTheirOwnEncoding()
    {
        var value = Section(Amqp1Descriptor.AmqpValue, 5u);
        var sequences = Join(Section(Amqp1Descriptor.AmqpSequence, new List<object?> { 1u }), Section(Amqp1Descriptor.AmqpSequence, new List<object?> { 2u }));

        CollectionAssert.AreEqual(value, Amqp1BusMessages.ToBusMessage(value, "news", false, Now).Payload.ToArray());
        CollectionAssert.AreEqual(sequences, Amqp1BusMessages.ToBusMessage(sequences, "news", false, Now).Payload.ToArray());
    }

    [TestMethod]
    [DataRow(0, DisplayName = "sections out of order")]
    [DataRow(1, DisplayName = "properties twice")]
    [DataRow(2, DisplayName = "application properties that are not a map")]
    [DataRow(3, DisplayName = "data that is not binary")]
    [DataRow(4, DisplayName = "a sequence that is not a list")]
    [DataRow(5, DisplayName = "two kinds of body")]
    public void ToBusMessage_MalformedMessage_Throws(int malformation)
    {
        var properties = Section(Amqp1Descriptor.Properties, new List<object?>());
        var data = Section(Amqp1Descriptor.Data, new byte[] { 1 });
        var message = malformation switch
        {
            0 => Join(data, properties),
            1 => Join(properties, properties),
            2 => Section(Amqp1Descriptor.ApplicationProperties, "x"),
            3 => Section(Amqp1Descriptor.Data, "x"),
            4 => Section(Amqp1Descriptor.AmqpSequence, "x"),
            _ => Join(data, Section(Amqp1Descriptor.AmqpValue, "x")),
        };

        Assert.ThrowsExactly<Amqp1DecodeException>(() => Amqp1BusMessages.ToBusMessage(message, "news", false, Now));
    }

    [TestMethod]
    public void ToTransferPayload_NativeFormNotRedelivered_IsItsBytes()
    {
        var native = Join(Section(Amqp1Descriptor.Header, new Amqp1Header { Durable = true }.ToDescribed().Value), Section(Amqp1Descriptor.Data, new byte[] { 1 }));
        var message = new BusMessage("news", new byte[] { 1 }) { NativeForm = new(Amqp1BusMessages.NativeFormat, native) };

        CollectionAssert.AreEqual(native, Amqp1BusMessages.ToTransferPayload(message, "/topic/news", redelivered: false, Now));
    }

    [TestMethod]
    public void ToTransferPayload_NativeFormRedelivered_RaisesTheHeadersDeliveryCount()
    {
        var data = Section(Amqp1Descriptor.Data, new byte[] { 1 });
        var withHeader = Join(Section(Amqp1Descriptor.Header, new Amqp1Header { Durable = true, FirstAcquirer = true, DeliveryCount = 2 }.ToDescribed().Value), data);
        var withoutHeader = data;

        var raised = HeaderOf(Amqp1BusMessages.ToTransferPayload(Native(withHeader), "/queue/q", redelivered: true, Now));
        var added = HeaderOf(Amqp1BusMessages.ToTransferPayload(Native(withoutHeader), "/queue/q", redelivered: true, Now));

        Assert.AreEqual(new Amqp1Header { Durable = true, DeliveryCount = 3 }, raised);
        Assert.AreEqual(new Amqp1Header { DeliveryCount = 1 }, added);
    }

    [TestMethod]
    public void ToTransferPayload_OtherMessage_IsBuiltFromTheBusFields()
    {
        var message = new BusMessage("news", "hi"u8.ToArray())
        {
            ExpiresAt = Now.AddSeconds(3),
            ContentType = "text/plain",
            ResponseTopic = "replies",
            CorrelationData = new byte[] { 7 },
            Headers = [new("a", "1"), new("a", "2"), new("b", "3")],
            PublishedAt = Now,
            NativeForm = new("amqp0", new byte[] { 1 }),
        };

        var sections = Amqp1MessageSection.ReadAll(Amqp1BusMessages.ToTransferPayload(message, "/topic/news", redelivered: true, Now));

        Assert.AreEqual(new Amqp1Header { Ttl = 3000, DeliveryCount = 1 }, Amqp1Header.FromDescribed(sections[0].Described));
        var properties = Amqp1Properties.FromDescribed(sections[1].Described);
        Assert.AreEqual("/topic/news", properties.To);
        Assert.AreEqual(new Amqp1Timestamp(Now.ToUnixTimeMilliseconds()), properties.CreationTime);
        Assert.AreEqual(new Amqp1Symbol("text/plain"), properties.ContentType);
        Assert.AreEqual("/topic/replies", properties.ReplyTo);
        CollectionAssert.AreEqual(new byte[] { 7 }, (byte[])properties.CorrelationId!);
        CollectionAssert.AreEqual(new[] { Entry("a", "1"), Entry("b", "3") }, ((Amqp1Map)sections[2].Described.Value!).Entries.ToArray());
        CollectionAssert.AreEqual("hi"u8.ToArray(), (byte[])sections[3].Described.Value!);
    }

    [TestMethod]
    public void ToTransferPayload_PlainMessage_HasPropertiesAndDataOnly()
    {
        var expired = new BusMessage("news", Array.Empty<byte>()) { ExpiresAt = Now.AddSeconds(-1) };
        var plain = new BusMessage("news", Array.Empty<byte>());

        var expiredSections = Amqp1MessageSection.ReadAll(Amqp1BusMessages.ToTransferPayload(expired, "/topic/news", redelivered: false, Now));
        var plainSections = Amqp1MessageSection.ReadAll(Amqp1BusMessages.ToTransferPayload(plain, "/queue/q", redelivered: false, Now));

        Assert.AreEqual(new Amqp1Header { Ttl = 0 }, Amqp1Header.FromDescribed(expiredSections[0].Described));
        CollectionAssert.AreEqual(
            new[] { Amqp1Descriptor.Properties, Amqp1Descriptor.Data },
            plainSections.Select(section => section.DescriptorCode).ToArray());
    }

    [TestMethod]
    public void ToTransferPayload_NullMessage_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1BusMessages.ToTransferPayload(null!, "/topic/news", false, Now));
    }

    private static BusMessage Native(byte[] bytes) => new("q", Array.Empty<byte>()) { NativeForm = new(Amqp1BusMessages.NativeFormat, bytes) };

    private static Amqp1Header HeaderOf(byte[] payload) => Amqp1Header.FromDescribed(Amqp1MessageSection.ReadAll(payload)[0].Described);

    private static byte[] CorrelatedBy(object correlationId) =>
        Section(Amqp1Descriptor.Properties, new Amqp1Properties { CorrelationId = correlationId }.ToDescribed().Value);

    private static string CorrelationHex(object correlationId) =>
        Convert.ToHexString(Amqp1BusMessages.ToBusMessage(CorrelatedBy(correlationId), "news", false, Now).CorrelationData!.Value.Span);

    private static KeyValuePair<object?, object?> Entry(object key, object value) => new(key, value);

    private static byte[] Section(ulong descriptor, object? value) => Amqp1Encoder.Encode(new Amqp1Described(descriptor, value));

    private static byte[] Join(params byte[][] sections) => [.. sections.SelectMany(section => section)];
}
