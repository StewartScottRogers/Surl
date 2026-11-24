using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>
/// ADR-0182, decision 6: the exchange log never holds the <c>PLAIN</c> or <c>AMQPLAIN</c> password
/// or a <c>secure-ok</c> response, whatever the chunking; every other byte is logged as received.
/// </summary>
[TestClass]
public sealed class Amqp0ExchangeLogRedactionTests
{
    private static readonly byte[] ProtocolHeader = "AMQP\0\0\u0009\u0001"u8.ToArray();

    private static IReceivedBytesRedactor NewRedactor() =>
        new Amqp0Broker(new UnitTestAmqp0AuthenticationPolicy(), new MessageBus.MessageBus(new ManualTimeProvider()))
            .CreateRedactor(new ExchangeRedactionContext(1, "amqp"));

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

    private static byte[] Text(string text) => Encoding.UTF8.GetBytes(text);

    private static void AssertLogged(byte[] expected, params byte[][] chunks) =>
        CollectionAssert.AreEqual(expected, Logged(chunks));

    [TestMethod]
    public void Plain_MasksOnlyThePassword() =>
        AssertLogged(
            Bytes(ProtocolHeader, StartOk("PLAIN", Text("\0alice\0****"))),
            Bytes(ProtocolHeader, StartOk("PLAIN", PlainResponse("alice", "s3cret"))));

    [TestMethod]
    public void Plain_SplitAtEveryByte_IsLoggedTheSameAsWhole()
    {
        var input = Bytes(ProtocolHeader, StartOkAnnouncingConsumerCancel("PLAIN", PlainResponse("alice", "s3cret")), TuneOk(0, 0, 0), Open("/"));
        var expected = Bytes(ProtocolHeader, StartOkAnnouncingConsumerCancel("PLAIN", Text("\0alice\0****")), TuneOk(0, 0, 0), Open("/"));
        for (var split = 0; split <= input.Length; split++)
        {
            AssertLogged(expected, input[..split], input[split..]);
        }
    }

    [TestMethod]
    public void Amqplain_MasksOnlyThePasswordValue()
    {
        byte[] reversed = [.. ShortString("password"), (byte)'S', .. LongString("s3cret"), .. ShortString("LOGIN"), (byte)'S', .. LongString("alice")];
        byte[] reversedMasked = [.. ShortString("password"), (byte)'S', .. LongString("****"), .. ShortString("LOGIN"), (byte)'S', .. LongString("alice")];

        AssertLogged(
            Bytes(ProtocolHeader, StartOk("AMQPLAIN", AmqplainResponse("alice", "****")), StartOk("AMQPLAIN", reversedMasked)),
            Bytes(ProtocolHeader, StartOk("AMQPLAIN", AmqplainResponse("alice", "s3cret")), StartOk("AMQPLAIN", reversed)));
    }

    [TestMethod]
    public void Amqplain_NotLongStringEntries_IsMaskedWhole()
    {
        byte[] otherType = [.. ShortString("LOGIN"), (byte)'I', 0, 0, 0, 1];
        byte[] cutValue = [.. ShortString("PASSWORD"), (byte)'S', 0, 0, 0, 9, 1];
        byte[] cutName = [9, 1];

        foreach (var response in new[] { otherType, cutValue, cutName })
        {
            AssertLogged(Bytes(ProtocolHeader, StartOk("AMQPLAIN", Text("****"))), Bytes(ProtocolHeader, StartOk("AMQPLAIN", response)));
        }
    }

    [TestMethod]
    public void Plain_WithoutTwoNuls_IsMaskedWhole()
    {
        foreach (var response in new[] { "s3cret", "\0s3cret" })
        {
            AssertLogged(Bytes(ProtocolHeader, StartOk("PLAIN", Text("****"))), Bytes(ProtocolHeader, StartOk("PLAIN", Text(response))));
        }
    }

    [TestMethod]
    public void OtherMechanism_IsMaskedWhole_AndAnEmptyResponseStaysEmpty() =>
        AssertLogged(
            Bytes(ProtocolHeader, StartOk("SCRAM-SHA-256", Text("****")), StartOk("EXTERNAL", [])),
            Bytes(ProtocolHeader, StartOk("SCRAM-SHA-256", Text("n,,n=alice,r=abc")), StartOk("EXTERNAL", [])));

    [TestMethod]
    public void SecureOk_IsMaskedWhole_AndAnEmptyOneStaysEmpty() =>
        AssertLogged(
            Bytes(ProtocolHeader, SecureOk("****"), SecureOk(string.Empty)),
            Bytes(ProtocolHeader, SecureOk("s3cret"), SecureOk(string.Empty)));

    [TestMethod]
    public void SecureOk_NotALongString_IsWrittenAsItsMethodThenMask()
    {
        AssertLogged(
            Bytes(ProtocolHeader, [1, 0, 0, 0, 0, 0, 9, 0, 10, 0, 21], Text("****")),
            Bytes(ProtocolHeader, Method(0, 10, 21, 0, 0, 0, 9, 1)));
        AssertLogged(
            Bytes(ProtocolHeader, [1, 0, 0, 0, 0, 0, 5, 0, 10, 0, 21], Text("****")),
            Bytes(ProtocolHeader, Method(0, 10, 21, 1)));
    }

    [TestMethod]
    public void StartOk_NotDecodable_IsWrittenAsItsMethodThenMask()
    {
        byte[][] undecodable =
        [
            [1],
            [.. Long(500)],
            [.. Long(0), 9, 1],
            [.. Long(0), 1, (byte)'P', .. Long(9), 1],
        ];
        foreach (var arguments in undecodable)
        {
            AssertLogged(
                Bytes(ProtocolHeader, [1, 0, 0, 0, 0, 0, (byte)(4 + arguments.Length), 0, 10, 0, 11], Text("****")),
                Bytes(ProtocolHeader, Method(0, 10, 11, arguments)));
        }
    }

    [TestMethod]
    public void StartOk_MechanismOrResponseLengthRunsPastTheFrame_IsWrittenAsItsMethodThenMask()
    {
        byte[][] pastTheEnd =
        [
            [.. Long(0), 5, (byte)'P', .. Long(0)],
            [.. Long(0), 1, (byte)'P', .. Long(9), .. Text("s3cret")],
        ];
        foreach (var arguments in pastTheEnd)
        {
            var logged = Logged(Bytes(ProtocolHeader, Method(0, 10, 11, arguments)));

            CollectionAssert.AreEqual(
                Bytes(ProtocolHeader, [1, 0, 0, 0, 0, 0, (byte)(4 + arguments.Length), 0, 10, 0, 11], Text("****")),
                logged);
        }
    }

    [TestMethod]
    public void FramesAfterTheLogin_AreLoggedAsReceived()
    {
        var publish = Method(1, 60, 40, [.. Short(0), .. ShortString(string.Empty), .. ShortString("q"), 0, .. Text("\0alice\0s3cret")]);
        var input = Bytes(ProtocolHeader, TuneOk(0, 0, 0), Open("/"), StartOk("PLAIN", PlainResponse("alice", "s3cret")), publish);

        AssertLogged(input, input);
    }

    [TestMethod]
    public void FramesThatCannotBeTheLogin_EndTheHolding()
    {
        var late = StartOk("PLAIN", PlainResponse("alice", "s3cret"));
        byte[][] frames =
        [
            Frame(8, 0, []),
            Method(1, 10, 11, 0),
            Method(0, 60, 40, 0),
            Frame(1, 0, [1, 2]),
        ];
        foreach (var frame in frames)
        {
            var input = Bytes(ProtocolHeader, frame, late);
            AssertLogged(input, input);
        }
    }

    [TestMethod]
    public void OversizedFrame_IsMasked_AndItsRestDropped()
    {
        var oversized = Method(0, 10, 11, new byte[Amqp0LoginBytesRedactor.MaxHeldBytes + 10]);
        var input = Bytes(ProtocolHeader, oversized, Open("/"));

        AssertLogged(Bytes(ProtocolHeader, Text("****"), Open("/")), input[..30000], input[30000..]);
    }

    [TestMethod]
    public void Flush_OfACutLoginFrame_KeepsOnlyTheMethodThenMasks()
    {
        var whole = StartOk("PLAIN", PlainResponse("alice", "s3cret"));
        AssertLogged(Bytes(ProtocolHeader, whole[..11], Text("****")), Bytes(ProtocolHeader, whole[..20]));
    }

    [TestMethod]
    public void Flush_OfACutOtherFrame_IsLoggedAsReceived()
    {
        var tuneOk = TuneOk(0, 0, 0);
        var basic = Method(0, 60, 40, 1, 2, 3, 4);
        foreach (var cut in new[] { tuneOk[..12], basic[..12], tuneOk[..11], tuneOk[..3], [] })
        {
            var input = Bytes(ProtocolHeader, cut);
            AssertLogged(input, input);
        }
    }

    [TestMethod]
    public void ObserveSent_ChangesNothing()
    {
        var redactor = NewRedactor();
        redactor.ObserveSent("anything"u8);
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(Bytes(ProtocolHeader, StartOk("PLAIN", PlainResponse("a", "b"))), output);

        CollectionAssert.AreEqual(Bytes(ProtocolHeader, StartOk("PLAIN", Text("\0a\0****"))), output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void NullOutput_Throws()
    {
        var redactor = NewRedactor();
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact(ProtocolHeader, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }
}
