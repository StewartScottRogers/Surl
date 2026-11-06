using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5TestPackets;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// Each MQTT 5.0 packet type, with every property table 2-4 allows in it, encoded and read
/// back one byte per read; and packets whose bytes are written by hand from chapter 3.
/// </summary>
[TestClass]
public sealed class Mqtt5PacketEncoderTests
{
    private static readonly Mqtt5PacketType[] PublishResponseTypes =
    [
        Mqtt5PacketType.PublishAcknowledgement,
        Mqtt5PacketType.PublishReceived,
        Mqtt5PacketType.PublishRelease,
        Mqtt5PacketType.PublishComplete,
    ];

    [TestMethod]
    public async Task Encode_ConnectWithWillUserNameAndPasswordAndEveryProperty_RoundTrips()
    {
        var will = new Mqtt5Will(2, true, EveryAllowedProperty(Mqtt5PropertyContext.Will), "will/topic", [9, 8]);
        var packet = new Mqtt5ConnectPacket(true, 60, EveryAllowedProperty(Mqtt5PropertyContext.Connect), "client", will, "user", [1, 2]);

        var decoded = (Mqtt5ConnectPacket)await RoundTripAsync(packet);

        Assert.IsTrue(decoded.CleanStart);
        Assert.AreEqual((ushort)60, decoded.KeepAlive);
        Assert.AreEqual("client", decoded.ClientIdentifier);
        Assert.AreEqual("user", decoded.UserName);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, decoded.Password);
        Assert.AreEqual(2, decoded.Will!.QualityOfService);
        Assert.IsTrue(decoded.Will.Retain);
        Assert.AreEqual("will/topic", decoded.Will.Topic);
        CollectionAssert.AreEqual(new byte[] { 9, 8 }, decoded.Will.Payload);
        AssertSameIdentifiers(will.Properties, decoded.Will.Properties);
        AssertSameIdentifiers(packet.Properties, decoded.Properties);
    }

    [TestMethod]
    public async Task Encode_ConnectWithoutWillOrCredentials_RoundTrips()
    {
        var packet = new Mqtt5ConnectPacket(false, 0, [], string.Empty, null, null, null);

        var decoded = (Mqtt5ConnectPacket)await RoundTripAsync(packet);

        Assert.IsFalse(decoded.CleanStart);
        Assert.IsNull(decoded.Will);
        Assert.IsNull(decoded.UserName);
        Assert.IsNull(decoded.Password);
    }

    [TestMethod]
    public async Task Encode_ConnectWithQualityOfService1WillAndPasswordOnly_RoundTrips()
    {
        var packet = new Mqtt5ConnectPacket(false, 5, [], "c", new Mqtt5Will(1, false, [], "t", []), null, [7]);

        var decoded = (Mqtt5ConnectPacket)await RoundTripAsync(packet);

        Assert.AreEqual(1, decoded.Will!.QualityOfService);
        Assert.IsFalse(decoded.Will.Retain);
        Assert.IsNull(decoded.UserName);
        CollectionAssert.AreEqual(new byte[] { 7 }, decoded.Password);
    }

    [TestMethod]
    public async Task Encode_ConnectAcknowledgementWithEveryProperty_RoundTrips()
    {
        var packet = new Mqtt5ConnectAcknowledgementPacket(
            true,
            Mqtt5ReasonCode.Success,
            EveryAllowedProperty(Mqtt5PropertyContext.ConnectAcknowledgement));

        var decoded = (Mqtt5ConnectAcknowledgementPacket)await RoundTripAsync(packet);

        Assert.IsTrue(decoded.SessionPresent);
        Assert.AreEqual(Mqtt5ReasonCode.Success, decoded.ReasonCode);
        AssertSameIdentifiers(packet.Properties, decoded.Properties);
    }

    [TestMethod]
    public async Task Encode_ConnectAcknowledgementRefused_RoundTripsWithoutSessionPresent()
    {
        var packet = new Mqtt5ConnectAcknowledgementPacket(false, Mqtt5ReasonCode.NotAuthorized, []);

        var decoded = (Mqtt5ConnectAcknowledgementPacket)await RoundTripAsync(packet);

        Assert.IsFalse(decoded.SessionPresent);
        Assert.AreEqual(Mqtt5ReasonCode.NotAuthorized, decoded.ReasonCode);
    }

    [TestMethod]
    [DataRow(0, false, false)]
    [DataRow(1, true, true)]
    [DataRow(2, false, true)]
    public async Task Encode_PublishWithEveryProperty_RoundTrips(int qualityOfService, bool duplicate, bool retain)
    {
        var packet = new Mqtt5PublishPacket(
            duplicate,
            qualityOfService,
            retain,
            "a/b",
            qualityOfService > 0 ? (ushort)7 : (ushort)0,
            EveryAllowedProperty(Mqtt5PropertyContext.Publish),
            [0x68, 0x69]);

        var decoded = (Mqtt5PublishPacket)await RoundTripAsync(packet);

        Assert.AreEqual(duplicate, decoded.Duplicate);
        Assert.AreEqual(qualityOfService, decoded.QualityOfService);
        Assert.AreEqual(retain, decoded.Retain);
        Assert.AreEqual("a/b", decoded.TopicName);
        Assert.AreEqual(packet.PacketIdentifier, decoded.PacketIdentifier);
        CollectionAssert.AreEqual(packet.Payload, decoded.Payload);
        AssertSameIdentifiers(packet.Properties, decoded.Properties);
    }

    [TestMethod]
    public async Task Encode_EachPublishResponseWithEveryProperty_RoundTrips()
    {
        foreach (var type in PublishResponseTypes)
        {
            var packet = new Mqtt5PublishResponsePacket(
                type,
                3,
                Mqtt5ReasonCode.NoMatchingSubscribers,
                EveryAllowedProperty(Mqtt5BodyReader.ContextOf(type)));

            var decoded = (Mqtt5PublishResponsePacket)await RoundTripAsync(packet);

            Assert.AreEqual(type, decoded.Type);
            Assert.AreEqual((ushort)3, decoded.PacketIdentifier);
            Assert.AreEqual(Mqtt5ReasonCode.NoMatchingSubscribers, decoded.ReasonCode);
            AssertSameIdentifiers(packet.Properties, decoded.Properties);
        }
    }

    [TestMethod]
    public async Task Encode_SubscribeWithEveryPropertyAndEveryOption_RoundTrips()
    {
        var packet = new Mqtt5SubscribePacket(
            1,
            EveryAllowedProperty(Mqtt5PropertyContext.Subscribe),
            [new Mqtt5Subscription("a/+", 2, true, true, 2), new Mqtt5Subscription("#", 0, false, false, 0)]);

        var decoded = (Mqtt5SubscribePacket)await RoundTripAsync(packet);

        Assert.AreEqual((ushort)1, decoded.PacketIdentifier);
        Assert.AreEqual(packet.Subscriptions[0], decoded.Subscriptions[0]);
        Assert.AreEqual(packet.Subscriptions[1], decoded.Subscriptions[1]);
        AssertSameIdentifiers(packet.Properties, decoded.Properties);
    }

    [TestMethod]
    [DataRow((int)Mqtt5PacketType.SubscribeAcknowledgement)]
    [DataRow((int)Mqtt5PacketType.UnsubscribeAcknowledgement)]
    public async Task Encode_ReasonCodeListWithEveryProperty_RoundTrips(int packetType)
    {
        var type = (Mqtt5PacketType)packetType;

        var packet = new Mqtt5ReasonCodeListPacket(
            type,
            9,
            EveryAllowedProperty(Mqtt5BodyReader.ContextOf(type)),
            [Mqtt5ReasonCode.GrantedQualityOfService1, Mqtt5ReasonCode.TopicFilterInvalid]);

        var decoded = (Mqtt5ReasonCodeListPacket)await RoundTripAsync(packet);

        Assert.AreEqual(type, decoded.Type);
        Assert.AreEqual((ushort)9, decoded.PacketIdentifier);
        CollectionAssert.AreEqual(packet.ReasonCodes.ToArray(), decoded.ReasonCodes.ToArray());
        AssertSameIdentifiers(packet.Properties, decoded.Properties);
    }

    [TestMethod]
    public async Task Encode_UnsubscribeWithEveryProperty_RoundTrips()
    {
        var packet = new Mqtt5UnsubscribePacket(4, EveryAllowedProperty(Mqtt5PropertyContext.Unsubscribe), ["a/b", "c"]);

        var decoded = (Mqtt5UnsubscribePacket)await RoundTripAsync(packet);

        Assert.AreEqual((ushort)4, decoded.PacketIdentifier);
        CollectionAssert.AreEqual(new[] { "a/b", "c" }, decoded.TopicFilters.ToArray());
        AssertSameIdentifiers(packet.Properties, decoded.Properties);
    }

    [TestMethod]
    [DataRow((int)Mqtt5PacketType.PingRequest)]
    [DataRow((int)Mqtt5PacketType.PingResponse)]
    public async Task Encode_Ping_RoundTrips(int packetType)
    {
        var type = (Mqtt5PacketType)packetType;

        var decoded = await RoundTripAsync(new Mqtt5PingPacket(type));

        Assert.AreEqual(type, decoded.Type);
    }

    [TestMethod]
    [DataRow((int)Mqtt5PacketType.Disconnect)]
    [DataRow((int)Mqtt5PacketType.Authentication)]
    public async Task Encode_ReasonCodeAndPropertiesWithEveryProperty_RoundTrips(int packetType)
    {
        var type = (Mqtt5PacketType)packetType;

        var packet = new Mqtt5ReasonCodeAndPropertiesPacket(
            type,
            Mqtt5ReasonCode.ContinueAuthentication,
            EveryAllowedProperty(Mqtt5BodyReader.ContextOf(type)));

        var decoded = (Mqtt5ReasonCodeAndPropertiesPacket)await RoundTripAsync(packet);

        Assert.AreEqual(type, decoded.Type);
        Assert.AreEqual(Mqtt5ReasonCode.ContinueAuthentication, decoded.ReasonCode);
        AssertSameIdentifiers(packet.Properties, decoded.Properties);
    }

    [TestMethod]
    public void Encode_MinimalConnect_WritesTheBytesOfSection3_1()
    {
        var packet = new Mqtt5ConnectPacket(
            true,
            60,
            [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.ReceiveMaximum, 20)],
            string.Empty,
            null,
            null,
            null);

        var bytes = Mqtt5PacketEncoder.Encode(packet);

        CollectionAssert.AreEqual(
            new byte[] { 0x10, 0x10, 0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, 0x05, 0x02, 0x00, 0x3C, 0x03, 0x21, 0x00, 0x14, 0x00, 0x00 },
            bytes);
    }

    [TestMethod]
    public void Encode_SuccessWithNoProperties_LeavesTheReasonCodeAndPropertyLengthOff()
    {
        var acknowledgement = new Mqtt5PublishResponsePacket(Mqtt5PacketType.PublishAcknowledgement, 1, Mqtt5ReasonCode.Success, []);
        var release = new Mqtt5PublishResponsePacket(Mqtt5PacketType.PublishRelease, 1, Mqtt5ReasonCode.PacketIdentifierNotFound, []);
        var disconnect = new Mqtt5ReasonCodeAndPropertiesPacket(Mqtt5PacketType.Disconnect, Mqtt5ReasonCode.Success, []);

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x02, 0x00, 0x01 }, Mqtt5PacketEncoder.Encode(acknowledgement));
        CollectionAssert.AreEqual(new byte[] { 0x62, 0x03, 0x00, 0x01, 0x92 }, Mqtt5PacketEncoder.Encode(release));
        CollectionAssert.AreEqual(new byte[] { 0xE0, 0x00 }, Mqtt5PacketEncoder.Encode(disconnect));
    }

    [TestMethod]
    public void Encode_PingRequest_WritesC000()
    {
        CollectionAssert.AreEqual(new byte[] { 0xC0, 0x00 }, Mqtt5PacketEncoder.Encode(new Mqtt5PingPacket(Mqtt5PacketType.PingRequest)));
    }

    [TestMethod]
    public void Encode_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Mqtt5PacketEncoder.Encode(null!));
    }

    private static async Task<Mqtt5Packet> RoundTripAsync(Mqtt5Packet packet)
    {
        var bytes = Mqtt5PacketEncoder.Encode(packet);

        var result = await ReadAsync(OneByteAtATime(bytes));

        Assert.AreEqual(Mqtt5PacketReadOutcome.PacketRead, result.Outcome, result.Fault?.Statement);
        CollectionAssert.AreEqual(bytes, Mqtt5PacketEncoder.Encode(result.Packet!));
        return result.Packet!;
    }

    private static void AssertSameIdentifiers(IReadOnlyList<Mqtt5Property> expected, IReadOnlyList<Mqtt5Property> actual)
    {
        Assert.IsNotEmpty(expected);
        CollectionAssert.AreEqual(
            expected.Select(property => property.Identifier).ToArray(),
            actual.Select(property => property.Identifier).ToArray());
    }
}
