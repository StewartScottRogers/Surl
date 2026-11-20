using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

[TestClass]
public sealed class NatsExchangeLogRedactionTests
{
    private static IReceivedBytesRedactor NewRedactor() =>
        new NatsServer(new UnitTestNatsAuthenticationPolicy(), UnitTestMessageBus.Create())
            .CreateRedactor(new ExchangeRedactionContext(1, "nats"));

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
    public void Connect_WithUserAndPass_MasksOnlyThePass() =>
        Assert.AreEqual(
            "CONNECT {\"verbose\":false,\"user\":\"alice\",\"pass\":\"****\",\"lang\":\"c\"}\r\n",
            Logged("CONNECT {\"verbose\":false,\"user\":\"alice\",\"pass\":\"s3cret\",\"lang\":\"c\"}\r\n"));

    [TestMethod]
    public void Connect_WithAuthToken_MasksTheToken() =>
        Assert.AreEqual("CONNECT {\"auth_token\":\"****\"}\r\n", Logged("CONNECT {\"auth_token\":\"tok\\\"en\"}\r\n"));

    [TestMethod]
    public void Connect_SplitAtEveryByte_IsLoggedTheSameAsWhole()
    {
        const string line = "CONNECT {\"user\":\"alice\",\"pass\":\"s3cret\"}\r\n";
        for (var split = 0; split <= line.Length; split++)
        {
            Assert.AreEqual("CONNECT {\"user\":\"alice\",\"pass\":\"****\"}\r\n", Logged(line[..split], line[split..]), $"split at {split}");
        }
    }

    [TestMethod]
    public void Connect_NamesMatchWithoutCaseAndAfterUnescaping() =>
        Assert.AreEqual(
            "connect \t{\"PASS\":\"****\",\"auth\u005ftoken\":\"****\"}\r\n",
            Logged("connect \t{\"PASS\":\"x\",\"auth\u005ftoken\":\"y\"}\r\n"));

    [TestMethod]
    public void Connect_SecretNotAString_IsMaskedAsAString() =>
        Assert.AreEqual(
            "CONNECT {\"pass\":\"****\",\"auth_token\":\"****\",\"echo\":true}\r\n",
            Logged("CONNECT {\"pass\":12345,\"auth_token\":{\"a\":[1]},\"echo\":true}\r\n"));

    [TestMethod]
    public void Connect_NestedPassAndOtherFields_AreLeftAlone() =>
        Assert.AreEqual(
            "CONNECT {\"x\":{\"pass\":\"keep\"},\"name\":\"n\"} tail\r\n",
            Logged("CONNECT {\"x\":{\"pass\":\"keep\"},\"name\":\"n\"} tail\r\n"));

    [TestMethod]
    public void Connect_NotReadableJson_IsMaskedWhole()
    {
        Assert.AreEqual("CONNECT ****\r\n", Logged("CONNECT {\"pass\":\"s3cret\"\r\n"));
        Assert.AreEqual("CONNECT ****\r\n", Logged("CONNECT s3cret\r\n"));
    }

    [TestMethod]
    public void OtherLines_AreLoggedAsReceived() =>
        Assert.AreEqual(
            "PING\r\nCONNECT\r\nCONNECT  \r\nCONNECTX {\"pass\":\"a\"}\r\nSUB a 1\r\nCON\r\n",
            Logged("PING\r\nCONNECT\r\nCONNECT  \r\nCONNECTX {\"pass\":\"a\"}\r\nSUB a 1\r\nCON\r\n"));

    [TestMethod]
    public void RuleObserveSent_ChangesNothing()
    {
        var redactor = NewRedactor();
        redactor.ObserveSent("INFO {}\r\n"u8);

        Assert.AreEqual("CONNECT {\"pass\":\"****\"}\r\n", Logged("CONNECT {\"pass\":\"a\"}\r\n"));
    }

    [TestMethod]
    public void Rule_NullOutput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new NatsConnectLineRedactionRule().Redact("PING"u8, null!));
}
