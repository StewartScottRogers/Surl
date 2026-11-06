namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// Performatives read from frame bodies and written into frames (AMQP 1.0 Part 2 section 2.7 and
/// Part 5 section 5.3.3; ADR-0107 decision 3).
/// </summary>
[TestClass]
public sealed class Amqp1FrameBodyTests
{
    private static readonly Amqp1Map Properties = new([new(new Amqp1Symbol("k"), "v")]);
    private static readonly Amqp1Symbol[] TwoCapabilities = [new("a"), new("b")];
    private static readonly Amqp1Error Error = new(Amqp1ErrorCondition.NotFound) { Description = "gone", Info = Properties };

    public static IEnumerable<object[]> EveryPerformativeWithEveryField =>
    [
        [new Amqp1Open("container") { Hostname = "host", MaxFrameSize = 65536, ChannelMax = 63, IdleTimeOut = 30000, OutgoingLocales = [new("en-US")], IncomingLocales = TwoCapabilities, OfferedCapabilities = TwoCapabilities, DesiredCapabilities = TwoCapabilities, Properties = Properties }],
        [new Amqp1Begin(1, 2048, 2048) { RemoteChannel = 0, HandleMax = 7, OfferedCapabilities = TwoCapabilities, DesiredCapabilities = TwoCapabilities, Properties = Properties }],
        [new Amqp1Attach("link", 1, true) { SenderSettleMode = 1, ReceiverSettleMode = 1, Source = new Amqp1Described(0x28ul, new object?[] { "queue" }), Target = new Amqp1Described(0x29ul, Array.Empty<object?>()), Unsettled = Properties, IncompleteUnsettled = true, InitialDeliveryCount = 0, MaxMessageSize = 1024, OfferedCapabilities = TwoCapabilities, DesiredCapabilities = TwoCapabilities, Properties = Properties }],
        [new Amqp1Flow(100, 1, 100) { NextIncomingId = 1, Handle = 0, DeliveryCount = 0, LinkCredit = 10, Available = 2, Drain = true, Echo = true, Properties = Properties }],
        [new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [1, 2], MessageFormat = 0, Settled = false, More = true, ReceiverSettleMode = 1, State = new Amqp1Described(0x24ul, Array.Empty<object?>()), Resume = true, Aborted = true, Batchable = true }],
        [new Amqp1Disposition(true, 0) { Last = 4, Settled = true, State = new Amqp1Described(0x24ul, Array.Empty<object?>()), Batchable = true }],
        [new Amqp1Detach(1) { Closed = true, Error = Error }],
        [new Amqp1End(Error)],
        [new Amqp1Close(Error)],
        [new Amqp1SaslMechanisms([new("PLAIN"), new("ANONYMOUS")])],
        [new Amqp1SaslInit(new("PLAIN")) { InitialResponse = [0, 117, 0, 112], Hostname = "host" }],
        [new Amqp1SaslChallenge([1])],
        [new Amqp1SaslResponse([2])],
        [new Amqp1SaslOutcome(1) { AdditionalData = [3] }],
    ];

    public static IEnumerable<object[]> EveryPerformativeWithOnlyItsMandatoryFields =>
    [
        [new Amqp1Open("c")],
        [new Amqp1Begin(0, 1, 1)],
        [new Amqp1Attach("l", 0, false)],
        [new Amqp1Flow(1, 0, 1)],
        [new Amqp1Transfer(0)],
        [new Amqp1Disposition(false, 0)],
        [new Amqp1Detach(0)],
        [new Amqp1End()],
        [new Amqp1Close()],
        [new Amqp1SaslMechanisms([new("PLAIN")])],
        [new Amqp1SaslInit(new("ANONYMOUS"))],
        [new Amqp1SaslOutcome(0)],
    ];

    [TestMethod]
    [DynamicData(nameof(EveryPerformativeWithEveryField))]
    [DynamicData(nameof(EveryPerformativeWithOnlyItsMandatoryFields))]
    public void Read_EveryPerformative_ReadsBackWhatWasWritten(Amqp1Performative performative)
    {
        var frame = Amqp1Frame.Carrying(0, performative);

        var body = Amqp1FrameBody.Read(frame);

        Assert.AreEqual(performative.GetType(), body.Performative!.GetType());
        Assert.AreEqual(performative.FrameType, frame.Type);
        CollectionAssert.AreEqual(Amqp1Encoder.Encode(performative.ToDescribed()), Amqp1Encoder.Encode(body.Performative.ToDescribed()));
        Assert.IsTrue(body.Payload.IsEmpty);
    }

    [TestMethod]
    public void Read_OpenWithTrailingNullFieldsOmitted_ReadsTheDefaults()
    {
        // open: descriptor smallulong 0x10, list8 of one field, container-id "c"
        var frame = new Amqp1Frame(Amqp1FrameType.Amqp, 0, Convert.FromHexString("005310C00401A10163"));

        var open = (Amqp1Open)Amqp1FrameBody.Read(frame).Performative!;

        Assert.AreEqual("c", open.ContainerId);
        Assert.IsNull(open.Hostname);
        Assert.AreEqual(uint.MaxValue, open.MaxFrameSize);
        Assert.AreEqual(ushort.MaxValue, open.ChannelMax);
        Assert.IsNull(open.IdleTimeOut);
        Assert.IsEmpty(open.OfferedCapabilities);
        Assert.IsNull(open.Properties);
    }

    [TestMethod]
    public void Read_AttachWithTrailingNullFieldsOmitted_ReadsTheDefaults()
    {
        // attach: name "l", handle uint0, role true; then two explicit nulls before the list ends
        var frame = new Amqp1Frame(Amqp1FrameType.Amqp, 0, Convert.FromHexString("005312C00805A1016C43414040"));

        var attach = (Amqp1Attach)Amqp1FrameBody.Read(frame).Performative!;

        Assert.IsTrue(attach.IsReceiver);
        Assert.AreEqual(Amqp1Attach.DefaultSenderSettleMode, attach.SenderSettleMode);
        Assert.AreEqual(Amqp1Attach.DefaultReceiverSettleMode, attach.ReceiverSettleMode);
        Assert.IsFalse(attach.IncompleteUnsettled);
        Assert.IsNull(attach.Source);
    }

    [TestMethod]
    public void Read_PerformativeWithASymbolicDescriptor_IsRead()
    {
        // amqp:begin:list, list8: remote-channel null, next-outgoing-id 1, incoming-window 2, outgoing-window 3
        var frame = new Amqp1Frame(Amqp1FrameType.Amqp, 0, Convert.FromHexString("00A30F616D71703A626567696E3A6C697374C00804405201520252" + "03"));

        var begin = (Amqp1Begin)Amqp1FrameBody.Read(frame).Performative!;

        Assert.AreEqual(1u, begin.NextOutgoingId);
        Assert.AreEqual(3u, begin.OutgoingWindow);
        Assert.IsNull(begin.RemoteChannel);
    }

    [TestMethod]
    public void Read_FieldsInWideAndSmallEncodings_AreReadAlike()
    {
        // flow in list32: next-incoming-id uint 1, incoming-window smalluint 2, next-outgoing-id uint0, outgoing-window uint 3
        var frame = new Amqp1Frame(Amqp1FrameType.Amqp, 0, Convert.FromHexString("005313D0000000110000000470000000015202437000000003"));

        var flow = (Amqp1Flow)Amqp1FrameBody.Read(frame).Performative!;

        Assert.AreEqual(1u, flow.NextIncomingId);
        Assert.AreEqual(2u, flow.IncomingWindow);
        Assert.AreEqual(0u, flow.NextOutgoingId);
        Assert.AreEqual(3u, flow.OutgoingWindow);
    }

    [TestMethod]
    public void Read_MultipleSymbolFieldAsOneSymbol_IsAListOfOne()
    {
        // sasl-mechanisms with sasl-server-mechanisms a single sym8 "PLAIN" rather than an array
        var frame = new Amqp1Frame(Amqp1FrameType.Sasl, 0, Convert.FromHexString("005340C00801A305504C41494E"));

        var mechanisms = (Amqp1SaslMechanisms)Amqp1FrameBody.Read(frame).Performative!;

        CollectionAssert.AreEqual(new[] { new Amqp1Symbol("PLAIN") }, mechanisms.ServerMechanisms.ToArray());
    }

    [TestMethod]
    public void Read_Transfer_ReturnsThePayloadAfterThePerformative()
    {
        var frame = Amqp1Frame.Carrying(1, new Amqp1Transfer(0) { DeliveryId = 0, DeliveryTag = [0] }, new byte[] { 0x00, 0x53, 0x77, 0x41 });

        var body = Amqp1FrameBody.Read(frame);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x53, 0x77, 0x41 }, body.Payload.ToArray());
    }

    [TestMethod]
    [DataRow("40", DisplayName = "not a described value")]
    [DataRow("0053104143", DisplayName = "a described value that is not a list")]
    [DataRow("00A101784500", DisplayName = "a string descriptor")]
    [DataRow("00A30178" + "45", DisplayName = "an unknown symbolic descriptor")]
    [DataRow("00539945", DisplayName = "an unknown numeric descriptor")]
    [DataRow("00534045", DisplayName = "a SASL performative in an AMQP frame")]
    public void Read_BodyThatIsNotAnAmqpPerformative_ThrowsFramingError(string hex)
    {
        var frame = new Amqp1Frame(Amqp1FrameType.Amqp, 0, Convert.FromHexString(hex));

        var exception = Assert.ThrowsExactly<Amqp1DecodeException>(() => Amqp1FrameBody.Read(frame));

        Assert.AreEqual(Amqp1ErrorCondition.FramingError, exception.Condition);
    }

    [TestMethod]
    public void Read_AmqpPerformativeInASaslFrame_ThrowsFramingError()
    {
        var frame = new Amqp1Frame(Amqp1FrameType.Sasl, 0, Convert.FromHexString("005318" + "45"));

        var exception = Assert.ThrowsExactly<Amqp1DecodeException>(() => Amqp1FrameBody.Read(frame));

        Assert.AreEqual(Amqp1ErrorCondition.FramingError, exception.Condition);
    }

    [TestMethod]
    [DataRow("005310" + "45", DisplayName = "open without its container-id")]
    [DataRow("005310C0020143", DisplayName = "open with a uint container-id")]
    [DataRow("005310C00502A1016343", DisplayName = "open with a uint hostname")]
    [DataRow("005310C00603A1016340" + "53", DisplayName = "open with a cut-short max-frame-size")]
    [DataRow("005310C00703A10163405301", DisplayName = "open with a ulong max-frame-size")]
    [DataRow("005310C00A06A10163404040405201", DisplayName = "open with a uint outgoing-locales")]
    [DataRow("005310C00C06A1016340404040E0020140", DisplayName = "open with an array of nulls for outgoing-locales")]
    [DataRow("005318C00201" + "41", DisplayName = "close with a boolean error")]
    [DataRow("005318C0050100531845", DisplayName = "close with an error of the close descriptor")]
    [DataRow("005318C0050100531D41", DisplayName = "close with an error that is not a list")]
    [DataRow("00531DC00201" + "40", DisplayName = "an error performative in an AMQP frame")]
    public void Read_FieldMissingOrOfTheWrongType_ThrowsDecodeErrorOrFramingError(string hex)
    {
        var frame = new Amqp1Frame(Amqp1FrameType.Amqp, 0, Convert.FromHexString(hex));

        Assert.ThrowsExactly<Amqp1DecodeException>(() => Amqp1FrameBody.Read(frame));
    }

    [TestMethod]
    public void Read_SaslMechanismsWithoutItsMechanisms_ThrowsDecodeError()
    {
        var frame = new Amqp1Frame(Amqp1FrameType.Sasl, 0, Convert.FromHexString("005340" + "45"));

        var exception = Assert.ThrowsExactly<Amqp1DecodeException>(() => Amqp1FrameBody.Read(frame));

        Assert.AreEqual(Amqp1ErrorCondition.DecodeError, exception.Condition);
    }

    [TestMethod]
    public void Read_OptionalValueOfTheWrongType_ThrowsDecodeError()
    {
        // begin with remote-channel a uint rather than a ushort
        var frame = new Amqp1Frame(Amqp1FrameType.Amqp, 0, Convert.FromHexString("005311C00904520152015201" + "5201"));

        var exception = Assert.ThrowsExactly<Amqp1DecodeException>(() => Amqp1FrameBody.Read(frame));

        Assert.AreEqual(Amqp1ErrorCondition.DecodeError, exception.Condition);
    }

    [TestMethod]
    public void Carrying_NullPerformative_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1Frame.Carrying(0, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1FrameBody.Read(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1Error.FromDescribed(null!));
    }
}
