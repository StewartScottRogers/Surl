using static Surl.MessageBus.PersistedBusFixture;

namespace Surl.MessageBus;

[TestClass]
public sealed class BusMessageCodecTests
{
    private const string Reason = "bad";
    private static readonly DateTimeOffset Eight = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Nine = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Write_AMessageWithNoOptionalProperty_IsTheExactBytes()
    {
        var message = Message("t", "hi") with { PublishedAt = Eight };

        var bytes = Encode(message);

        CollectionAssert.AreEqual(Bytes("00000001 74 00 00 08df3a499fec8000 00000000 00000002 6869"), bytes);
    }

    [TestMethod]
    public void Write_AMessageWithEveryProperty_IsTheExactBytes()
    {
        var bytes = Encode(Full());

        CollectionAssert.AreEqual(
            Bytes("00000003 612f62 ff 02 08df3a499fec8000 08df3a5201b0e800 00000001 63 00000001 72 00000001 01 00000001 02 00000005 616d717031 00000002 0a0b 00000001 00000001 68 00000001 76 00000001 78"),
            bytes);
    }

    [TestMethod]
    public void Read_AMessageWithEveryProperty_ReadsBackEqual()
    {
        var read = Decode(Encode(Full()));

        AssertSame(Full(), read);
    }

    [TestMethod]
    public void Read_AMessageWithNoOptionalProperty_ReadsBackWithEveryOptionalPropertyNull()
    {
        var message = Message("t", string.Empty) with { QualityOfService = BusQualityOfService.AtLeastOnce, PublishedAt = Eight };

        var read = Decode(Encode(message));

        AssertSame(message, read);
    }

    [TestMethod]
    [DataRow("00000001 74 80 00 08df3a499fec8000 00000000 00000000", DisplayName = "The native form flag is set and the native form runs into the headers")]
    [DataRow("00000001 74 00 03 08df3a499fec8000 00000000 00000000", DisplayName = "The quality of service is 3")]
    [DataRow("00000001 74 00 00 8000000000000000 00000000 00000000", DisplayName = "The time is past the largest 8-byte value")]
    [DataRow("00000001 74 00 00 2bca2875f4374000 00000000 00000000", DisplayName = "The time is past the last tick")]
    [DataRow("00000001 74 00 00 08df3a499fec8000 00000000 00000005 61", DisplayName = "The payload runs past the end")]
    [DataRow("80000000 74", DisplayName = "A length is past the largest count")]
    [DataRow("00000001 ff 00 00 08df3a499fec8000 00000000 00000000", DisplayName = "The topic is not UTF-8")]
    [DataRow("000000", DisplayName = "A length is cut short")]
    public void Read_AMalformedMessage_ThrowsWithTheReadersReason(string hex)
    {
        var exception = Assert.ThrowsExactly<InvalidDataException>(() => Decode(Bytes(hex)));

        Assert.AreEqual(Reason, exception.Message);
    }

    [TestMethod]
    public void ReadShortString_ATwoByteLengthAndUtf8_ReadsTheString()
    {
        var reader = new BusFileReader(Bytes("0002 6869"), [], Reason);

        Assert.AreEqual("hi", reader.ReadShortString());
        Assert.IsTrue(reader.AtEnd);
    }

    [TestMethod]
    public void BusFileReader_BytesWithoutTheHeader_ThrowsWithTheReason()
    {
        var exception = Assert.ThrowsExactly<InvalidDataException>(() => new BusFileReader(Bytes("01"), Bytes("02"), Reason));

        Assert.AreEqual(Reason, exception.Message);
    }

    private static BusMessage Full() => Message("a/b", "x") with
    {
        Retain = true,
        Utf8Payload = true,
        QualityOfService = BusQualityOfService.ExactlyOnce,
        PublishedAt = Eight,
        ExpiresAt = Nine,
        ContentType = "c",
        ResponseTopic = "r",
        CorrelationData = new byte[] { 1 },
        Key = new byte[] { 2 },
        Headers = [new("h", "v")],
        NativeForm = new("amqp1", new byte[] { 0x0A, 0x0B }),
    };

    private static byte[] Encode(BusMessage message)
    {
        var writer = new BusFileWriter();
        BusMessageCodec.Write(writer, message);
        return writer.ToArray();
    }

    private static BusMessage Decode(byte[] bytes) => BusMessageCodec.Read(new BusFileReader(bytes, [], Reason));

    private static void AssertSame(BusMessage expected, BusMessage actual)
    {
        Assert.AreEqual(expected.Topic, actual.Topic);
        CollectionAssert.AreEqual(expected.Payload.ToArray(), actual.Payload.ToArray());
        Assert.AreEqual(expected.Retain, actual.Retain);
        Assert.AreEqual(expected.Utf8Payload, actual.Utf8Payload);
        Assert.AreEqual(expected.QualityOfService, actual.QualityOfService);
        Assert.AreEqual(expected.PublishedAt, actual.PublishedAt);
        Assert.AreEqual(expected.ExpiresAt, actual.ExpiresAt);
        Assert.AreEqual(expected.ContentType, actual.ContentType);
        Assert.AreEqual(expected.ResponseTopic, actual.ResponseTopic);
        CollectionAssert.AreEqual(expected.CorrelationData?.ToArray(), actual.CorrelationData?.ToArray());
        CollectionAssert.AreEqual(expected.Key?.ToArray(), actual.Key?.ToArray());
        CollectionAssert.AreEqual(expected.Headers.ToArray(), actual.Headers.ToArray());
        Assert.AreEqual(expected.NativeForm?.Format, actual.NativeForm?.Format);
        CollectionAssert.AreEqual(expected.NativeForm?.Bytes.ToArray(), actual.NativeForm?.Bytes.ToArray());
    }

    /// <summary>Compares two messages property by property, payload and byte fields by value.</summary>
    internal static void AssertEquivalent(BusMessage expected, BusMessage actual) => AssertSame(expected, actual);
}
