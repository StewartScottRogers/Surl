using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// ADR-0182: the password of a SASL <c>PLAIN</c> <c>sasl-init</c> is logged as <c>****</c>, the user
/// name and everything else as received. The frames are written by hand from AMQP 1.0 Part 2 section
/// 2.3 and Part 5 section 5.3.3.2.
/// </summary>
[TestClass]
public sealed class Amqp1ExchangeLogRedactionTests
{
    private static readonly byte[] SaslHeader = Encoding.ASCII.GetBytes("AMQP\x03\x01\x00\x00");
    private static readonly byte[] TlsHeader = Encoding.ASCII.GetBytes("AMQP\x02\x01\x00\x00");
    private static readonly byte[] AmqpHeader = Encoding.ASCII.GetBytes("AMQP\x00\x01\x00\x00");
    private static readonly byte[] InitDescriptor = [0x00, 0x53, 0x41];

    private static readonly byte[] PlainInit = Init("PLAIN", "\0alice\0s3cret");
    private static readonly byte[] PlainInitMasked = Replace(PlainInit, "s3cret");

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    private static byte[] Replace(byte[] frame, string secret) =>
        Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(frame).Replace(secret, "****", StringComparison.Ordinal));

    private static byte[] Frame(byte[] body, byte dataOffset = 2, byte type = 1)
    {
        var frame = new byte[(dataOffset * 4) + body.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)frame.Length);
        frame[4] = dataOffset;
        frame[5] = type;
        body.CopyTo(frame, dataOffset * 4);
        return frame;
    }

    private static byte[] Variable(byte narrowCode, byte[] value, bool wide)
    {
        if (!wide)
        {
            return Concat([narrowCode, (byte)value.Length], value);
        }

        var length = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)value.Length);
        return Concat([(byte)(narrowCode + 0x10)], length, value);
    }

    private static byte[] Init(string mechanism, string response, bool wide = false)
    {
        var fields = Concat(
            Variable(0xA3, Encoding.ASCII.GetBytes(mechanism), wide),
            Variable(0xA0, Encoding.Latin1.GetBytes(response), wide));
        var list = wide
            ? Concat([0xD0], BitConverter.GetBytes(fields.Length + 4).Reverse().ToArray(), [0, 0, 0, 2])
            : [0xC0, (byte)(fields.Length + 1), 2];
        return Frame(Concat(InitDescriptor, list, fields));
    }

    private static byte[] Logged(params byte[][] chunks)
    {
        var redactor = new Amqp1SaslInitBytesRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(chunk, output);
        }

        redactor.ObserveSent([1, 2, 3]);
        redactor.Flush(output);
        return output.WrittenSpan.ToArray();
    }

    private static void AssertLogged(byte[] expected, params byte[][] chunks) => CollectionAssert.AreEqual(expected, Logged(chunks));

    private static void AssertUnchanged(byte[] frame) => AssertLogged(Concat(SaslHeader, frame), Concat(SaslHeader, frame));

    [TestMethod]
    public void PlainInit_Password_IsLoggedMaskedAndTheUserNameStays()
    {
        AssertLogged(Concat(SaslHeader, PlainInitMasked), Concat(SaslHeader, PlainInit));
        Assert.IsTrue(Encoding.Latin1.GetString(PlainInitMasked).Contains("alice", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PlainInit_SplitAtEveryByte_IsMaskedTheSame()
    {
        var all = Concat(SaslHeader, PlainInit, AmqpHeader);
        var expected = Concat(SaslHeader, PlainInitMasked, AmqpHeader);
        for (var split = 1; split < all.Length; split++)
        {
            AssertLogged(expected, all[..split], all[split..]);
        }
    }

    [TestMethod]
    public void PlainInit_OneByteAtATime_IsMaskedTheSame() =>
        AssertLogged(Concat(SaslHeader, PlainInitMasked), [.. Concat(SaslHeader, PlainInit).Select(value => new[] { value })]);

    [TestMethod]
    public void PlainInit_InTheWideForms_IsMasked() =>
        AssertLogged(
            Concat(SaslHeader, Replace(Init("PLAIN", "\0alice\0s3cret", wide: true), "s3cret")),
            Concat(SaslHeader, Init("PLAIN", "\0alice\0s3cret", wide: true)));

    [TestMethod]
    public void PlainInit_WithOneNul_MasksWhatFollowsIt() =>
        AssertLogged(
            Concat(SaslHeader, Replace(Init("PLAIN", "alice\0s3cret"), "s3cret")),
            Concat(SaslHeader, Init("PLAIN", "alice\0s3cret")));

    [TestMethod]
    public void PlainInit_WithNoNul_MasksTheWholeResponse() =>
        AssertLogged(Concat(SaslHeader, Replace(Init("PLAIN", "s3cret"), "s3cret")), Concat(SaslHeader, Init("PLAIN", "s3cret")));

    [TestMethod]
    public void PlainInit_WithAnEmptyPassword_IsLoggedAsReceived() => AssertUnchanged(Init("PLAIN", "\0alice\0"));

    [TestMethod]
    public void PlainInit_WithAnEmptyResponse_IsLoggedAsReceived() => AssertUnchanged(Init("PLAIN", string.Empty));

    [TestMethod]
    public void OtherMechanismInit_IsLoggedAsReceived() => AssertUnchanged(Init("SCRAM-SHA-256", "n,,n=alice,r=abc"));

    [TestMethod]
    public void TwoFrames_AreEachJudged()
    {
        var anonymous = Init("ANONYMOUS", "trace");
        AssertLogged(Concat(SaslHeader, anonymous, PlainInitMasked), Concat(SaslHeader, anonymous, PlainInit));
    }

    [TestMethod]
    public void SaslResponse_IsLoggedAsReceived() =>
        AssertUnchanged(Frame(Concat([0x00, 0x53, 0x43, 0xC0, 0x04, 0x01], Variable(0xA0, "secret"u8.ToArray(), wide: false))));

    [TestMethod]
    public void AmqpFrameTypeInTheSaslStage_IsLoggedAsReceived() => AssertUnchanged(Frame(Concat(InitDescriptor, [0xC0, 0x01, 0x00]), type: 0));

    [TestMethod]
    public void InitWithAnEmptyList_IsLoggedAsReceived() => AssertUnchanged(Frame(Concat(InitDescriptor, [0x45])));

    [TestMethod]
    public void InitWithADataOffsetBelowTwo_IsLoggedAsReceived() => AssertUnchanged(Frame(Concat(InitDescriptor, [0xC0, 0x01, 0x00]), dataOffset: 1));

    [TestMethod]
    public void InitWithADataOffsetPastTheFrame_IsLoggedAsReceived()
    {
        var frame = Frame(InitDescriptor);
        frame[4] = 200;
        AssertUnchanged(frame);
    }

    [TestMethod]
    public void InitWhoseMechanismIsNotASymbol_IsLoggedAsReceived() =>
        AssertUnchanged(Frame(Concat(InitDescriptor, [0xC0, 0x02, 0x01, 0x40])));

    [TestMethod]
    public void InitWithNoFieldsAfterTheList_IsLoggedAsReceived() =>
        AssertUnchanged(Frame(Concat(InitDescriptor, [0xC0, 0x01, 0x00])));

    [TestMethod]
    public void InitWhoseResponseIsNull_IsLoggedAsReceived() =>
        AssertUnchanged(Frame(Concat(InitDescriptor, [0xC0, 0x09, 0x02], Variable(0xA3, "PLAIN"u8.ToArray(), wide: false), [0x40])));

    [TestMethod]
    public void InitWhoseMechanismLengthRunsPastTheFrame_IsLoggedAsReceived() =>
        AssertUnchanged(Frame(Concat(InitDescriptor, [0xC0, 0x05, 0x02, 0xA3, 0x7F, 0x41, 0x42, 0x43])));

    [TestMethod]
    public void InitWhoseWideLengthRunsPastTheFrame_IsLoggedAsReceived() =>
        AssertUnchanged(Frame(Concat(InitDescriptor, [0xC0, 0x08, 0x02, 0xB3, 0x00, 0x00, 0x10, 0x00, 0x41])));

    [TestMethod]
    public void InitWhoseWideLengthHeaderIsCutOff_IsLoggedAsReceived() =>
        AssertUnchanged(Frame(Concat(InitDescriptor, [0xC0, 0x03, 0x02, 0xB3, 0x00])));

    [TestMethod]
    public void InitWhoseNarrowLengthIsCutOff_IsLoggedAsReceived() =>
        AssertUnchanged(Frame(Concat(InitDescriptor, [0xC0, 0x02, 0x02, 0xA3])));

    [TestMethod]
    public void InitWithAListCodeCutOff_IsLoggedAsReceived() => AssertUnchanged(Frame(InitDescriptor));

    [TestMethod]
    public void TlsHeaderThenSaslHeader_StillRedacts() =>
        AssertLogged(Concat(TlsHeader, SaslHeader, PlainInitMasked), Concat(TlsHeader, SaslHeader, PlainInit));

    [TestMethod]
    public void AmqpHeaderFirst_EverythingAfterIsLoggedAsReceived()
    {
        var bytes = Concat(AmqpHeader, PlainInit);
        AssertLogged(bytes, bytes);
    }

    [TestMethod]
    public void BytesThatAreNotAProtocolHeader_AreLoggedAsReceived()
    {
        var bytes = Concat([0x16, 0x03, 0x01], PlainInit);
        AssertLogged(bytes, bytes);
        AssertLogged("AMX"u8.ToArray(), "AMX"u8.ToArray());
    }

    [TestMethod]
    public void AFrameSizeBelowEightOrAboveTheSaslLimit_EndsRedaction()
    {
        var tiny = Concat(SaslHeader, [0, 0, 0, 4], PlainInit);
        AssertLogged(tiny, tiny);
        var huge = Concat(SaslHeader, [0, 1, 0, 1], PlainInit);
        AssertLogged(huge, huge);
    }

    [TestMethod]
    public void PlainInitOver512Bytes_Password_IsLoggedMasked()
    {
        var password = new string('s', 700);
        var init = Init("PLAIN", "\0alice\0" + password, wide: true);

        AssertLogged(Concat(SaslHeader, Replace(init, password)), Concat(SaslHeader, init));
    }

    [TestMethod]
    public void AmqpHeaderAfterSasl_EndsRedaction()
    {
        AssertLogged(
            Concat(SaslHeader, PlainInitMasked, AmqpHeader, PlainInit),
            Concat(SaslHeader, PlainInit, AmqpHeader, PlainInit));
    }

    [TestMethod]
    public void PlainInitCutOffByTheEnd_IsLoggedUpToItsDescriptorThenMasked()
    {
        var cut = Concat(SaslHeader, PlainInit[..^3]);
        AssertLogged(Concat(SaslHeader, PlainInit[..11], "****"u8.ToArray()), cut);
    }

    [TestMethod]
    public void AnotherFrameCutOffByTheEnd_IsLoggedAsReceived()
    {
        var cut = Concat(SaslHeader, Frame([0x00, 0x53, 0x43, 0xC0, 0x04, 0x01, 0xA0, 0x01, 0x41])[..12]);
        AssertLogged(cut, cut);
        AssertLogged(Concat(SaslHeader, [0, 0, 0]), Concat(SaslHeader, [0, 0, 0]));
        AssertLogged(Concat(SaslHeader, PlainInit[..9]), Concat(SaslHeader, PlainInit[..9]));
        AssertLogged(Concat(SaslHeader, Frame(InitDescriptor, type: 0)[..11]), Concat(SaslHeader, Frame(InitDescriptor, type: 0)[..11]));
    }

    [TestMethod]
    public void NoBytes_AreLoggedAsNothing() => AssertLogged([]);

    [TestMethod]
    public void NullOutput_IsRefused()
    {
        var redactor = new Amqp1SaslInitBytesRedactor();
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([1], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }

    [TestMethod]
    public void TheContainer_CreatesTheRedactor()
    {
        var container = new Amqp1Container(new UnitTestAmqp1SaslPolicy("PLAIN"), new MessageBus.MessageBus(new ManualTimeProvider()));
        var redactor = container.CreateRedactor(new ExchangeRedactionContext(1, "amqp1"));
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(Concat(SaslHeader, PlainInit), output);
        CollectionAssert.AreEqual(Concat(SaslHeader, PlainInitMasked), output.WrittenSpan.ToArray());
    }
}
