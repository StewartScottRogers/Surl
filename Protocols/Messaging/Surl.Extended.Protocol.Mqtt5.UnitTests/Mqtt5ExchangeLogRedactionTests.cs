using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// ADR-0182, decision 6: the exchange log never holds the MQTT 5 <c>CONNECT</c> password or an
/// Authentication Data value, whatever the chunking; every other byte is logged as received.
/// </summary>
[TestClass]
public sealed class Mqtt5ExchangeLogRedactionTests
{
    private static readonly byte[] Secret = Encoding.UTF8.GetBytes("s3cret");
    private static readonly byte[] Token = [0xAA, 0xBB, 0xCC];
    private static readonly byte[] Mask = "****"u8.ToArray();

    private static IReceivedBytesRedactor NewRedactor() =>
        new Mqtt5Broker(
            Mqtt5BrokerExchange.NewState(),
            new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted))
            .CreateRedactor(new ExchangeRedactionContext(1, "mqtt5"));

    private static byte[] Logged(params byte[][] chunks)
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(chunk, output);
        }

        redactor.Flush(output);
        return output.WrittenSpan.ToArray();
    }

    private static byte[] Bytes(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    private static byte[] Connect(
        byte[]? password,
        string? userName = "alice",
        IReadOnlyList<Mqtt5Property>? properties = null,
        Mqtt5Will? will = null) =>
        Mqtt5PacketEncoder.Encode(new Mqtt5ConnectPacket(true, 30, properties ?? [], "c1", will, userName, password));

    private static Mqtt5Property AuthenticationData(byte[] value) =>
        Mqtt5Property.FromBinary(Mqtt5PropertyIdentifier.AuthenticationData, value);

    private static Mqtt5Property AuthenticationMethod() =>
        Mqtt5Property.FromText(Mqtt5PropertyIdentifier.AuthenticationMethod, "SCRAM-SHA-256");

    private static byte[] Authentication(params Mqtt5Property[] properties) =>
        Mqtt5PacketEncoder.Encode(new Mqtt5ReasonCodeAndPropertiesPacket(Mqtt5PacketType.Authentication, Mqtt5ReasonCode.ContinueAuthentication, properties));

    private static readonly byte[] PingRequest = [0xC0, 0x00];

    private static readonly byte[] PublishHoldingThePassword = [0x30, 0x0A, 0x00, 0x01, (byte)'t', 0x00, .. Secret];

    [TestMethod]
    public void Connect_MasksOnlyThePassword() =>
        CollectionAssert.AreEqual(Connect(Mask), Logged(Connect(Secret)));

    [TestMethod]
    public void Connect_KeepsTheUserNameClientIdentifierWillAndOtherProperties()
    {
        var will = new Mqtt5Will(1, true, [Mqtt5Property.FromUserProperty("k", "v")], "will/topic", [1, 2, 3]);
        Mqtt5Property[] properties = [AuthenticationMethod(), AuthenticationData(Token), Mqtt5Property.FromUserProperty("a", "b")];

        var logged = Logged(Connect(Secret, "alice", properties, will));

        Mqtt5Property[] maskedProperties = [AuthenticationMethod(), AuthenticationData(Mask), Mqtt5Property.FromUserProperty("a", "b")];
        CollectionAssert.AreEqual(Connect(Mask, "alice", maskedProperties, will), logged);
    }

    [TestMethod]
    public void Connect_WithAuthenticationDataAndNoPassword_MasksTheData() =>
        CollectionAssert.AreEqual(
            Connect(null, null, [AuthenticationMethod(), AuthenticationData(Mask)]),
            Logged(Connect(null, null, [AuthenticationMethod(), AuthenticationData(Token)])));

    [TestMethod]
    public void Connect_WithNoSecret_IsLoggedAsReceived() =>
        CollectionAssert.AreEqual(Connect(null, "alice"), Logged(Connect(null, "alice")));

    [TestMethod]
    public void Connect_SplitAtEveryByte_IsLoggedTheSameAsWhole()
    {
        Mqtt5Property[] properties = [AuthenticationMethod(), AuthenticationData(Token)];
        var input = Bytes(Connect(Secret, "alice", properties), PingRequest, PublishHoldingThePassword);
        var expected = Bytes(Connect(Mask, "alice", [AuthenticationMethod(), AuthenticationData(Mask)]), PingRequest, PublishHoldingThePassword);
        for (var split = 0; split <= input.Length; split++)
        {
            CollectionAssert.AreEqual(expected, Logged(input[..split], input[split..]));
        }
    }

    [TestMethod]
    public void Connect_OneByteAtATime_IsLoggedTheSameAsWhole()
    {
        var input = Connect(Secret);

        CollectionAssert.AreEqual(Connect(Mask), Logged([.. input.Select(value => new[] { value })]));
    }

    [TestMethod]
    public void Authentication_MasksTheAuthenticationData() =>
        CollectionAssert.AreEqual(
            Bytes(Authentication(AuthenticationMethod(), AuthenticationData(Mask)), PingRequest),
            Logged(Bytes(Authentication(AuthenticationMethod(), AuthenticationData(Token)), PingRequest)));

    [TestMethod]
    public void Authentication_WithNoPropertiesOrNoBody_IsLoggedAsReceived()
    {
        byte[] reasonOnly = [0xF0, 0x01, 0x18];
        byte[] empty = [0xF0, 0x00];

        CollectionAssert.AreEqual(Bytes(reasonOnly, empty, Authentication()), Logged(Bytes(reasonOnly, empty, Authentication())));
    }

    [TestMethod]
    public void OtherPackets_AreLoggedAsReceived_EvenWhenTheyHoldThePasswordBytes()
    {
        var input = Bytes(Connect(Secret), PublishHoldingThePassword, PingRequest);

        var logged = Logged(input[..40], input[40..]);

        CollectionAssert.AreEqual(Bytes(Connect(Mask), PublishHoldingThePassword, PingRequest), logged);
    }

    [TestMethod]
    public void ObserveSent_ChangesNothing()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();

        redactor.ObserveSent([0x20, 0x03, 0x00, 0x00, 0x00]);
        redactor.Redact(Connect(Secret), output);

        CollectionAssert.AreEqual(Connect(Mask), output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void Connect_ThatDoesNotDecode_IsWrittenAsItsFirstByteAndAMask()
    {
        byte[] masked = [0x10, 0x04, .. Mask];
        byte[] truncated = [0x10, 0x03, 0x00, 0x04, 0x4D];
        byte[] unknownProperty = [0x10, 0x0D, 0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, 0x05, 0x02, 0x00, 0x1E, 0x01, 0x7F, 0x00];
        byte[] trailing = [.. Connect(null, null)[2..], 0x01];

        foreach (var body in new[] { truncated[2..], unknownProperty[2..], trailing })
        {
            CollectionAssert.AreEqual(masked, Logged([0x10, (byte)body.Length, .. body]));
        }
    }

    [TestMethod]
    public void Connect_OverTheHeldLimit_IsMaskedAndTheRestDropped()
    {
        byte[] header = [0x10, 0xF0, 0xA2, 0x04];
        var body = new byte[70_000];

        CollectionAssert.AreEqual(
            Bytes([0x10, 0x04], Mask, PingRequest),
            Logged(header, body[..30_000], body[30_000..], PingRequest));
    }

    [TestMethod]
    public void FixedHeaderWithAnOverlongRemainingLength_PassesTheRestThrough()
    {
        byte[] notMqtt = [0x10, 0x80, 0x80, 0x80, 0x80, 0x01, 0x02];

        CollectionAssert.AreEqual(Bytes(notMqtt, Connect(Secret)), Logged(notMqtt[..3], Bytes(notMqtt[3..], Connect(Secret))));
    }

    [TestMethod]
    public void Flush_OfAConnectCutShort_WritesItsFirstByteAndAMask()
    {
        var connect = Connect(Secret);

        CollectionAssert.AreEqual(Bytes([0x10, 0x04], Mask), Logged(connect[..10]));
    }

    [TestMethod]
    public void Flush_OfACutFixedHeader_WritesItAsReceived()
    {
        byte[] cutHeader = [0x10, 0x80];

        CollectionAssert.AreEqual(cutHeader, Logged(cutHeader));
    }

    [TestMethod]
    public void Redact_AfterFlush_PassesBytesThrough()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Flush(output);

        redactor.Redact(Connect(Secret), output);

        CollectionAssert.AreEqual(Connect(Secret), output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void NullOutput_Throws()
    {
        var redactor = NewRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([1], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }
}
