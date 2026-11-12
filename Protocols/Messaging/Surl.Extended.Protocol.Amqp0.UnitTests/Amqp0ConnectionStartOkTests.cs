using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary><c>connection.start-ok</c> and the <c>AMQPLAIN</c> response (ADR-0112 decision 2).</summary>
[TestClass]
public sealed class Amqp0ConnectionStartOkTests
{
    [TestMethod]
    public void Read_Arguments_ReadsEachField()
    {
        byte[] arguments = [.. Long(0), .. ShortString("PLAIN"), .. LongString("\0a\0b"), .. ShortString("en_US")];

        var startOk = Amqp0ConnectionStartOk.Read(arguments);

        Assert.IsEmpty(startOk.ClientProperties.Fields);
        Assert.AreEqual("PLAIN", startOk.Mechanism);
        CollectionAssert.AreEqual(PlainResponse("a", "b"), startOk.Response.ToArray());
        Assert.AreEqual("en_US", startOk.Locale);
    }

    private static Amqp0ConnectionStartOk StartOkWith(params Amqp0Field[] properties) => new(new(properties), "PLAIN", ReadOnlyMemory<byte>.Empty, "en_US");

    private static Amqp0Field Capabilities(params Amqp0Field[] capabilities) => new("capabilities", Amqp0FieldValue.Table(new(capabilities)));

    [TestMethod]
    public void AnnouncesConsumerCancelNotify_CapabilitiesSetItTrue_IsTrue() =>
        Assert.IsTrue(StartOkWith(Capabilities(new Amqp0Field("consumer_cancel_notify", Amqp0FieldValue.Boolean(true)))).AnnouncesConsumerCancelNotify);

    [TestMethod]
    public void AnnouncesConsumerCancelNotify_NoCapabilities_IsFalse() =>
        Assert.IsFalse(StartOkWith().AnnouncesConsumerCancelNotify);

    [TestMethod]
    public void AnnouncesConsumerCancelNotify_CapabilitiesNotATable_IsFalse() =>
        Assert.IsFalse(StartOkWith(new Amqp0Field("capabilities", Amqp0FieldValue.Boolean(true))).AnnouncesConsumerCancelNotify);

    [TestMethod]
    public void AnnouncesConsumerCancelNotify_FlagAbsent_IsFalse() =>
        Assert.IsFalse(StartOkWith(Capabilities(new Amqp0Field("basic.nack", Amqp0FieldValue.Boolean(true)))).AnnouncesConsumerCancelNotify);

    [TestMethod]
    public void AnnouncesConsumerCancelNotify_FlagFalse_IsFalse() =>
        Assert.IsFalse(StartOkWith(Capabilities(new Amqp0Field("consumer_cancel_notify", Amqp0FieldValue.Boolean(false)))).AnnouncesConsumerCancelNotify);

    [TestMethod]
    public void AnnouncesConsumerCancelNotify_FlagNotABoolean_IsFalse() =>
        Assert.IsFalse(StartOkWith(Capabilities(new Amqp0Field("consumer_cancel_notify", Amqp0FieldValue.Int32(1)))).AnnouncesConsumerCancelNotify);

    [TestMethod]
    public void ToPlainResponse_LoginAndPassword_IsNulLoginNulPassword() =>
        CollectionAssert.AreEqual(PlainResponse("alice", "secret"), Amqp0ConnectionStartOk.ToPlainResponse(AmqplainResponse("alice", "secret")));

    public static IEnumerable<object[]> MalformedResponses =>
    [
        [new byte[] { 0x05 }],
        [(byte[])[.. ShortString("LOGIN"), (byte)'S', .. LongString("alice")]],
        [(byte[])[.. ShortString("LOGIN"), (byte)'S', .. LongString("al\0ice"), .. ShortString("PASSWORD"), (byte)'S', .. LongString("x")]],
        [(byte[])[.. ShortString("LOGIN"), (byte)'S', .. LongString("alice"), .. ShortString("PASSWORD"), (byte)'I', 0, 0, 0, 1]],
    ];

    [TestMethod]
    [DynamicData(nameof(MalformedResponses))]
    public void ToPlainResponse_AnyOtherShape_IsNull(byte[] response) =>
        Assert.IsNull(Amqp0ConnectionStartOk.ToPlainResponse(response));
}
