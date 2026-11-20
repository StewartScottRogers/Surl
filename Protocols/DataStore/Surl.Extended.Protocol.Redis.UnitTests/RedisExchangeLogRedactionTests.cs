using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// What the exchange log shows of <c>AUTH</c> and <c>HELLO ... AUTH</c> (ADR-0182).
/// </summary>
[TestClass]
public sealed class RedisExchangeLogRedactionTests
{
    private static string Logged(params string[] chunks)
    {
        var redactor = new RedisReceivedBytesRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(Encoding.Latin1.GetBytes(chunk), output);
        }

        redactor.ObserveSent("+OK\r\n"u8);
        redactor.Flush(output);
        return Encoding.Latin1.GetString(output.WrittenSpan);
    }

    private static string OneChunkPerByte(string text) => Logged([.. text.Select(character => character.ToString())]);

    private const string Masked = "$4\r\n****\r\n";

    [TestMethod]
    public void CreateRedactor_ReturnsTheRedisRedactor() =>
        Assert.IsInstanceOfType<RedisReceivedBytesRedactor>(
            new RedisServer(new UnitTestRedisAuthenticationPolicy(true)).CreateRedactor(new ExchangeRedactionContext(1, "redis")));

    [TestMethod]
    public void Resp_AuthPassword_IsMasked() =>
        Assert.AreEqual("*2\r\n$4\r\nAUTH\r\n" + Masked, Logged("*2\r\n$4\r\nAUTH\r\n$6\r\ns3cret\r\n"));

    [TestMethod]
    public void Resp_AuthUserAndPassword_KeepsTheUserAndMasksThePassword() =>
        Assert.AreEqual("*3\r\n$4\r\nauth\r\n$5\r\nalice\r\n" + Masked, Logged("*3\r\n$4\r\nauth\r\n$5\r\nalice\r\n$6\r\ns3cret\r\n"));

    [TestMethod]
    public void Resp_AuthWithTooManyArguments_MasksEveryArgument() =>
        Assert.AreEqual("*4\r\n$4\r\nAUTH\r\n" + Masked + Masked + Masked, Logged("*4\r\n$4\r\nAUTH\r\n$1\r\na\r\n$1\r\nb\r\n$1\r\nc\r\n"));

    [TestMethod]
    public void Resp_AuthWithNoArgument_IsLoggedAsSent() =>
        Assert.AreEqual("*1\r\n$4\r\nAUTH\r\n", Logged("*1\r\n$4\r\nAUTH\r\n"));

    [TestMethod]
    public void Resp_HelloAuth_MasksThePassword() =>
        Assert.AreEqual(
            "*5\r\n$5\r\nHELLO\r\n$1\r\n3\r\n$4\r\nAUTH\r\n$5\r\nalice\r\n" + Masked,
            Logged("*5\r\n$5\r\nHELLO\r\n$1\r\n3\r\n$4\r\nAUTH\r\n$5\r\nalice\r\n$6\r\ns3cret\r\n"));

    [TestMethod]
    public void Resp_HelloWithoutAuth_IsLoggedAsSent() =>
        Assert.AreEqual("*2\r\n$5\r\nHELLO\r\n$1\r\n3\r\n", Logged("*2\r\n$5\r\nHELLO\r\n$1\r\n3\r\n"));

    [TestMethod]
    public void Resp_SplitAtEveryByte_IsMaskedTheSame()
    {
        const string sent = "*3\r\n$4\r\nAUTH\r\n$5\r\nalice\r\n$6\r\ns3cret\r\n";
        Assert.AreEqual(Logged(sent), OneChunkPerByte(sent));
        Assert.IsFalse(OneChunkPerByte(sent).Contains("s3cret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Resp_SetWithAPasswordLookingValue_IsLoggedAsSent() =>
        Assert.AreEqual("*3\r\n$3\r\nSET\r\n$1\r\nk\r\n$6\r\ns3cret\r\n", Logged("*3\r\n$3\r\nSET\r\n$1\r\nk\r\n$6\r\ns3cret\r\n"));

    [TestMethod]
    public void Resp_EmptyAndNullElements_AreLoggedAsSent() =>
        Assert.AreEqual(
            "*3\r\n$4\r\nECHO\r\n$0\r\n\r\n$-1\r\n",
            Logged("*3\r\n$4\r\nECHO\r\n$0\r\n\r\n$-1\r\n"));

    [TestMethod]
    public void Resp_LongCommandName_IsNotAnAuthCommand()
    {
        var name = new string('A', 40);
        var sent = "*2\r\n$40\r\n" + name + "\r\n$6\r\ns3cret\r\n";
        Assert.AreEqual(sent, Logged(sent));
    }

    [TestMethod]
    public void Resp_PipelinedCommands_AreEachRedacted() =>
        Assert.AreEqual(
            "*1\r\n$4\r\nPING\r\n*2\r\n$4\r\nAUTH\r\n" + Masked + "*1\r\n$4\r\nPING\r\n",
            Logged("*1\r\n$4\r\nPING\r\n*2\r\n$4\r\nAUTH\r\n$6\r\ns3cret\r\n*1\r\n$4\r\nPING\r\n"));

    [TestMethod]
    [DataRow("*0\r\n")]
    [DataRow("*-1\r\n")]
    [DataRow("*x\r\n")]
    [DataRow("*2\n")]
    [DataRow("*2\r\nPING\r\n")]
    [DataRow("*2\r\n$x\r\n")]
    [DataRow("*2\r\n$-5\r\n")]
    public void Resp_MalformedFraming_IsLoggedAsSent(string sent) => Assert.AreEqual(sent, Logged(sent));

    [TestMethod]
    public void Resp_HeaderThatNeverEnds_IsLoggedAsSent()
    {
        var sent = "*" + new string('1', 30);
        Assert.AreEqual(sent, Logged(sent));
        var element = "*2\r\n$" + new string('1', 30);
        Assert.AreEqual(element, Logged(element));
    }

    [TestMethod]
    public void Resp_AuthEvenAfterAMalformedCommand_IsMasked() =>
        Assert.AreEqual("*0\r\n*2\r\n$4\r\nAUTH\r\n" + Masked, Logged("*0\r\n*2\r\n$4\r\nAUTH\r\n$6\r\ns3cret\r\n"));

    [TestMethod]
    public void Inline_AuthPassword_IsMasked() => Assert.AreEqual("auth ****\r\n", Logged("auth s3cret\r\n"));

    [TestMethod]
    public void Inline_AuthUserAndPassword_KeepsTheUser() =>
        Assert.AreEqual("AUTH alice ****\r\n", Logged("AUTH alice \"s3 cret\"\r\n"));

    [TestMethod]
    public void Inline_AuthWithTooManyArguments_MasksThemAll() =>
        Assert.AreEqual("AUTH **** **** ****\r\n", Logged("AUTH a b c\r\n"));

    [TestMethod]
    public void Inline_AuthSplitAcrossReads_IsMasked() =>
        Assert.AreEqual("AUTH ****\r\n", OneChunkPerByte("AUTH s3cret\r\n"));

    [TestMethod]
    public void Inline_AuthEndedByABareLineFeed_IsMaskedAndKeepsItsEnding() =>
        Assert.AreEqual("AUTH ****\n", Logged("AUTH s3cret\n"));

    [TestMethod]
    public void Inline_AuthWithNoArgument_IsLoggedAsSent() => Assert.AreEqual("AUTH\r\n", Logged("AUTH\r\n"));

    [TestMethod]
    public void Inline_BlankLine_IsLoggedAsSent() => Assert.AreEqual("\r\n", Logged("\r\n"));

    [TestMethod]
    public void Inline_BareLineFeedOnAnEmptyLine_IsLoggedAsSent() => Assert.AreEqual("\n", Logged("\n"));

    [TestMethod]
    public void Inline_HelloAuth_MasksThePassword() =>
        Assert.AreEqual("HELLO 3 AUTH alice ****\r\n", Logged("HELLO 3 AUTH alice s3cret\r\n"));

    [TestMethod]
    public void Inline_HelloWithoutAuthOrWithAnIncompleteOne_IsLoggedAsSent() =>
        Assert.AreEqual("HELLO 3\r\nHELLO 3 AUTH alice\r\nHELLO AUTH\r\n", Logged("HELLO 3\r\nHELLO 3 AUTH alice\r\nHELLO AUTH\r\n"));

    [TestMethod]
    public void Inline_SetWithAPasswordLookingValue_IsLoggedAsSent() =>
        Assert.AreEqual("SET k s3cret\r\n", Logged("SET k s3cret\r\n"));

    [TestMethod]
    public void Inline_UnbalancedQuotesAfterAuthOrHello_MaskEverythingAfterTheCommand() =>
        Assert.AreEqual("AUTH ****\r\nhello ****\r\n", Logged("AUTH \"s3cret\r\nhello 3 AUTH a \"b\r\n"));

    [TestMethod]
    public void Inline_UnbalancedQuotesAfterAnotherCommand_AreLoggedAsSent() =>
        Assert.AreEqual("SET \"s3cret\r\nSET\"\r\n", Logged("SET \"s3cret\r\nSET\"\r\n"));

    [TestMethod]
    public void Inline_LineEndingWithTheCommandWordOnly_IsLoggedAsSent() =>
        Assert.AreEqual("PING\r\n", Logged("PING\r\n"));

    [TestMethod]
    public void Inline_UnfinishedLineAtFlush_IsRedactedWithoutAnEnding() =>
        Assert.AreEqual("AUTH ****", Logged("AUTH s3c"));

    [TestMethod]
    public void Flush_UnfinishedHeader_IsLoggedAsSent() => Assert.AreEqual("*2\r\n$4", Logged("*2\r\n$4"));

    [TestMethod]
    public void Flush_InTheMiddleOfAPassword_WritesNothingOfIt() =>
        Assert.AreEqual("*2\r\n$4\r\nAUTH\r\n" + Masked, Logged("*2\r\n$4\r\nAUTH\r\n$6\r\ns3c"));

    [TestMethod]
    public void Flush_AfterACompleteCommand_WritesNothingMore() => Assert.AreEqual("*1\r\n$4\r\nPING\r\n", Logged("*1\r\n$4\r\nPING\r\n"));

    [TestMethod]
    public void Inline_LineLongerThanTheLimit_IsMaskedAndDiscardedToItsEnd()
    {
        var logged = Logged(new string('x', LineBytesRedactor.DefaultMaxHeldBytes + 10), "more\r\nPING\r\n");
        Assert.AreEqual("****\r\nPING\r\n", logged);
    }

    [TestMethod]
    public void Redact_NullOutput_Throws()
    {
        var redactor = new RedisReceivedBytesRedactor();
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact("x"u8, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }
}
