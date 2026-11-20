using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

[TestClass]
public sealed class Pop3ExchangeLogRedactionTests
{
    private static string Logged(params string[] chunks) => Run(chunks.Select(chunk => (true, chunk)).ToArray());

    // (isReceived, text): received chunks are redacted, sent chunks are observed.
    private static string Run((bool IsReceived, string Text)[] steps)
    {
        var redactor = Pop3TestExchange.Server(
            Pop3TestExchange.AccountStore(new ManualTimeProvider()))
            .CreateRedactor(new ExchangeRedactionContext(1, "pop3"));
        var output = new ArrayBufferWriter<byte>();
        foreach (var (isReceived, text) in steps)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            if (isReceived)
            {
                redactor.Redact(bytes, output);
            }
            else
            {
                redactor.ObserveSent(bytes);
            }
        }

        redactor.Flush(output);
        return Encoding.ASCII.GetString(output.WrittenSpan);
    }

    [TestMethod]
    public void Pass_IsLoggedWithItsArgumentMasked() =>
        Assert.AreEqual("PASS ****\r\n", Logged("PASS s3cret\r\n"));

    [TestMethod]
    public void Pass_SplitAcrossReads_IsMasked() =>
        Assert.AreEqual("PASS ****\r\n", Logged("PA", "SS s3c", "ret\r\n"));

    [TestMethod]
    public void Pass_InLowerCase_IsMaskedKeepingTheClientsCase() =>
        Assert.AreEqual("pass ****\r\n", Logged("pass s3cret\r\n"));

    [TestMethod]
    public void PassWithNoArgument_IsLoggedAsSent() =>
        Assert.AreEqual("PASS\r\nPASS \r\n", Logged("PASS\r\nPASS \r\n"));

    [TestMethod]
    public void UserApopAndMaildropCommands_AreLoggedUnchanged() =>
        Assert.AreEqual(
            "USER alice\r\nAPOP alice c4c9334bac560ecc979e58001b3e22fb\r\nRETR 1\r\nPASSX s3cret\r\n",
            Logged("USER alice\r\nAPOP alice c4c9334bac560ecc979e58001b3e22fb\r\nRETR 1\r\nPASSX s3cret\r\n"));

    [TestMethod]
    public void AuthPlainInitialResponse_IsMasked() =>
        Assert.AreEqual("AUTH PLAIN ****\r\n", Logged("AUTH PLAIN AGFsaWNlAHMzY3JldA==\r\n"));

    [TestMethod]
    public void AuthLoginContinuations_AreMasked() =>
        Assert.AreEqual(
            "AUTH LOGIN\r\n****\r\n****\r\n",
            Run([(true, "AUTH LOGIN\r\n"), (false, "+ VXNlcm5hbWU6\r\n"), (true, "YWxpY2U=\r\n"), (false, "+ UGFzc3dvcmQ6\r\n"), (true, "czNjcmV0\r\n")]));

    [TestMethod]
    public void RetrReplyAndLinesAfterAnOkReply_AreLoggedUnchanged() =>
        Assert.AreEqual(
            "RETR 1\r\nUSER alice\r\n",
            Run([(true, "RETR 1\r\n"), (false, "+OK 133 octets\r\nFrom: a\r\n.\r\n"), (true, "USER alice\r\n")]));

    [TestMethod]
    public void PassAnsweringAContinuation_IsMaskedAndTheMarkIsConsumed() =>
        Assert.AreEqual(
            "PASS ****\r\nUSER x\r\n",
            Run([(false, "+ \r\n"), (true, "PASS s3cret\r\n"), (true, "USER x\r\n")]));
}
