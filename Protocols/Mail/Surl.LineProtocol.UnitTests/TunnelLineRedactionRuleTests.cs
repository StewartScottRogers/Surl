using System.Buffers;

namespace Surl.LineProtocol;

[TestClass]
public sealed class TunnelLineRedactionRuleTests
{
    private static string Redact(TunnelLineRedactionRule rule, string line)
    {
        var output = new ArrayBufferWriter<byte>();
        rule.Redact(Wire.Bytes(line), output);

        return Wire.Text(output.WrittenSpan);
    }

    // What the rule writes for the answer once the origin has sent each of the reads.
    private static string AnswerAfter(string answer, params string[] originReads)
    {
        var rule = new TunnelLineRedactionRule();
        foreach (var read in originReads)
        {
            rule.ObserveSent(Wire.Bytes(read));
        }

        return Redact(rule, answer);
    }

    [TestMethod]
    public void Redact_NullOutput_IsRefused() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new TunnelLineRedactionRule().Redact([1], null!));

    [TestMethod]
    [DataRow("EHLO client", "EHLO client")]
    [DataRow("PASS s3cret-pw", "PASS ****")]
    [DataRow("a LOGIN app s3cret-pw", "a LOGIN app ****")]
    [DataRow("AUTH PLAIN AGFwcABzM2NyZXQtcHc=", "AUTH PLAIN ****")]
    [DataRow("a AUTHENTICATE PLAIN AGFwcABzM2NyZXQtcHc=", "a AUTHENTICATE PLAIN ****")]
    [DataRow("X-Api-Key: s3cret", "X-Api-Key: ****")]
    [DataRow("AUTH alice \"a b\"", "AUTH alice ****")]
    [DataRow("AUTH alice 'a b'", "AUTH alice ****")]
    [DataRow("AUTH alice x\"a b\"", "AUTH alice ****")]
    [DataRow("AUTH \"al ice\" \"a b\"", "AUTH \"al ****")]
    [DataRow("AUTH \"alice\" s3cret", "AUTH \"alice\" ****")]
    public void Redact_Line_IsLoggedAsTheRuleThatWithholdsWritesIt(string line, string logged) =>
        Assert.AreEqual(logged, Redact(new TunnelLineRedactionRule(), line));

    [TestMethod]
    [DataRow("AUTH\valice\vs3cret", "AUTH\v****")]
    [DataRow("AUTH\falice\fs3cret", "AUTH\f****")]
    [DataRow("AUTH\ralice\rs3cret", "AUTH\r****")]
    [DataRow("AUTH \"alice\" s3cret", "AUTH \"alice\" ****")]
    [DataRow("\"AUTH\" alice s3cret", "\"AUTH\" ****")]
    [DataRow("AUTH alice \v s3cret", "AUTH ****")]
    [DataRow("AUTH \v alice s3cret", "AUTH ****")]
    [DataRow("AUTH * \v s3cret", "AUTH ****")]
    [DataRow("\"AUTH\" login s3cret", "\"AUTH\" ****")]
    [DataRow("AUTH s3cret", "AUTH ****")]
    [DataRow("AUTH \"s3 cret\"", "AUTH ****")]
    [DataRow("HELLO 3 AUTH alice s3cret", "HELLO 3 AUTH ****")]
    [DataRow("HELLO 3 SETNAME app AUTH alice s3cret", "HELLO 3 SETNAME app AUTH ****")]
    public void Redact_RedisAuthLineAfterAnotherLine_ShowsNoByteOfThePassword(string line, string logged)
    {
        var rule = new TunnelLineRedactionRule();
        Redact(rule, "PING");

        Assert.AreEqual(logged, Redact(rule, line));
    }

    [TestMethod]
    public void Redact_RedisHelloAuthAsTheFirstLine_ShowsNoByteOfThePassword() =>
        Assert.AreEqual("HELLO 3 AUTH ****", Redact(new TunnelLineRedactionRule(), "HELLO 3 AUTH alice s3cret"));

    [TestMethod]
    [DataRow("AUTH PLAIN")]
    [DataRow("AUTH LOGIN")]
    [DataRow("AUTH TLS")]
    public void Redact_AuthLineNamingOnlyAMechanism_IsWrittenAsReceived(string line) =>
        Assert.AreEqual(line, Redact(new TunnelLineRedactionRule(), line));

    [TestMethod]
    public void Redact_LineTwoRulesWithhold_IsLoggedAsTheOneWithholdingFromEarliestWritesIt() =>
        Assert.AreEqual(
            "Authorization: Login ****",
            Redact(new TunnelLineRedactionRule(), "Authorization: Login c2Vj cmV0"));

    [TestMethod]
    public void Redact_LineEndedByALineFeed_KeepsTheLineFeedAfterTheMask() =>
        Assert.AreEqual("PASS ****\n", Redact(new TunnelLineRedactionRule(), "PASS s3cret-pw\n"));

    [TestMethod]
    [DataRow("", "a NOOP")]
    [DataRow("\n", "****")]
    public void Redact_LiteralOverSeveralLines_CountsTheBytesThatEndedEachLine(string lineFeed, string loggedLastLine)
    {
        // Eight bytes: a line the caller took CR LF from counts 5, one that still ends with its line feed 4.
        var rule = new TunnelLineRedactionRule();
        Redact(rule, "a LOGIN app {8}");
        Redact(rule, "s3c" + lineFeed);
        Redact(rule, "ret" + lineFeed);

        Assert.AreEqual(loggedLastLine, Redact(rule, "a NOOP"));
    }

    [TestMethod]
    [DataRow("334 UGFzc3dvcmQ6\r\n")]
    [DataRow("334\r\n")]
    [DataRow("+ \r\n")]
    [DataRow("+\r\n")]
    [DataRow("+\n")]
    [DataRow("250 OK\r\n+\r\n")]
    public void ObserveSent_Continuation_MasksTheNextLine(string sent) =>
        Assert.AreEqual("****", AnswerAfter("czNjcmV0LXB3", sent));

    [TestMethod]
    [DataRow("250 OK\r\n")]
    [DataRow("334")]
    [DataRow("+OK\r\n")]
    [DataRow("")]
    public void ObserveSent_NoContinuation_LeavesTheNextLine(string sent) =>
        Assert.AreEqual("QUIT", AnswerAfter("QUIT", sent));

    [TestMethod]
    public void ObserveSent_ContinuationSplitAtEveryByte_MasksTheNextLine()
    {
        const string sent = "250 OK\r\n334 UGFzc3dvcmQ6\r\n";
        foreach (var reads in Wire.Splits(sent))
        {
            Assert.AreEqual("****", AnswerAfter("czNjcmV0LXB3", reads), string.Join('|', reads));
        }
    }
}
