using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Stomp.UnitTests;

[TestClass]
public sealed class StompExchangeLogRedactionTests
{
    private const string Connect = "CONNECT\naccept-version:1.2\nlogin:alice\npasscode:s3cret\nhost:h\n\n\0";
    private const string ConnectLogged = "CONNECT\naccept-version:1.2\nlogin:alice\npasscode:****\nhost:h\n\n\0";

    private static IReceivedBytesRedactor NewRedactor() =>
        new StompServer(new UnitTestStompAuthenticationPolicy(), new Surl.MessageBus.MessageBus(TimeProvider.System))
            .CreateRedactor(new ExchangeRedactionContext(1, "stomp"));

    private static string Logged(params string[] chunks)
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(Encoding.UTF8.GetBytes(chunk), output);
        }

        redactor.Flush(output);
        return Encoding.UTF8.GetString(output.WrittenSpan);
    }

    [TestMethod]
    public void Connect_MasksOnlyThePasscode() => Assert.AreEqual(ConnectLogged, Logged(Connect));

    [TestMethod]
    public void Stomp_WithCrLf_MasksThePasscodeAndKeepsTheLineEnds() =>
        Assert.AreEqual(
            "\n\r\nSTOMP\r\nlogin:alice\r\npasscode:****\r\n\r\n\0",
            Logged("\n\r\nSTOMP\r\nlogin:alice\r\npasscode:pw\r\n\r\n\0"));

    [TestMethod]
    public void Connect_SplitAtEveryByte_IsLoggedTheSameAsWhole()
    {
        for (var split = 0; split <= Connect.Length; split++)
        {
            Assert.AreEqual(ConnectLogged, Logged(Connect[..split], Connect[split..]), $"split at {split}");
        }
    }

    [TestMethod]
    public void Connect_OneByteAtATime_IsMasked() =>
        Assert.AreEqual(ConnectLogged, Logged([.. Connect.Select(c => c.ToString())]));

    [TestMethod]
    public void Connect_EveryPasscodeHeaderIsMasked_NamesMatchExactly() =>
        Assert.AreEqual(
            "CONNECT\npasscode:****\nPasscode:kept\npasscode2:kept\npasscode:****\nnocolon\n\n",
            Logged("CONNECT\npasscode:a\nPasscode:kept\npasscode2:kept\npasscode:b\nnocolon\n\n"));

    [TestMethod]
    public void AfterTheConnectHeaders_EverythingIsLoggedAsReceived() =>
        Assert.AreEqual(
            ConnectLogged + "SEND\npasscode:body\n\nCONNECT\npasscode:x\n\n\0",
            Logged(Connect + "SEND\npasscode:body\n\nCONNECT\npasscode:x\n\n\0"));

    [TestMethod]
    public void FirstFrameNotConnect_IsLoggedAsReceived() =>
        Assert.AreEqual("SEND\npasscode:x\n\n\0", Logged("SEND\npasscode:x\n\n\0"));

    [TestMethod]
    public void UnfinishedLine_AtFlush_IsMaskedWhenItIsThePasscode()
    {
        Assert.AreEqual("CONNECT\npasscode:****", Logged("CONNECT\npasscode:sec"));
        Assert.AreEqual("CONNECT\nlogin:al", Logged("CONNECT\nlogin:al"));
        Assert.AreEqual("passcode:x", Logged("passcode:x"));
    }

    [TestMethod]
    public void LineOverTheLimit_IsMaskedAndDroppedToItsLineFeed()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(Encoding.UTF8.GetBytes("CONNECT\n" + new string('a', StompConnectPasscodeRedactor.MaxHeldBytes + 5)), output);
        redactor.Redact("more\npasscode:z\n"u8, output);
        redactor.Flush(output);

        Assert.AreEqual("CONNECT\n****\npasscode:****\n", Encoding.UTF8.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void FlushInTheMiddleOfADiscardedLine_ForgetsIt()
    {
        var redactor = new StompConnectPasscodeRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(Encoding.UTF8.GetBytes(new string('a', StompConnectPasscodeRedactor.MaxHeldBytes + 1)), output);
        redactor.Flush(output);
        redactor.Redact("\n"u8, output);

        Assert.AreEqual("****\n", Encoding.UTF8.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void ObserveSent_ChangesNothing()
    {
        var redactor = NewRedactor();
        redactor.ObserveSent("CONNECTED\n\n\0"u8);

        Assert.AreEqual(ConnectLogged, Logged(Connect));
    }

    [TestMethod]
    public void NullOutput_Throws()
    {
        var redactor = new StompConnectPasscodeRedactor();
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact("x"u8, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }
}
