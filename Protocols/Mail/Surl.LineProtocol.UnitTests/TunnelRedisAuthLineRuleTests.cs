using System.Buffers;

namespace Surl.LineProtocol;

[TestClass]
public sealed class TunnelRedisAuthLineRuleTests
{
    private static string Redact(string line)
    {
        var output = new ArrayBufferWriter<byte>();
        new TunnelRedisAuthLineRule().Redact(Wire.Bytes(line), output);

        return Wire.Text(output.WrittenSpan);
    }

    [TestMethod]
    public void Redact_NullOutput_IsRefused() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new TunnelRedisAuthLineRule().Redact([1], null!));

    [TestMethod]
    [DataRow("AUTH\valice\vs3cret", "AUTH\v****")]
    [DataRow("auth\falice\fs3cret", "auth\f****")]
    [DataRow("AUTH\ralice\rs3cret", "AUTH\r****")]
    [DataRow("AUTH alice\vs3cret", "AUTH ****")]
    [DataRow("\v\f AUTH alice s3cret", "\v\f AUTH ****")]
    [DataRow("AUTH\vs3cret", "AUTH\v****")]
    [DataRow("\"AUTH\" alice s3cret", "\"AUTH\" ****")]
    [DataRow("'auth' alice s3cret", "'auth' ****")]
    [DataRow("A\"UTH\"\talice s3cret", "A\"UTH\"\t****")]
    [DataRow("\"AUTH\"\valice\vs3cret", "\"AUTH\"\v****")]
    [DataRow("\"\\x41UTH\" alice s3cret", "\"\\x41UTH\" ****")]
    [DataRow("  \"AUTH\"  alice s3cret", "  \"AUTH\" ****")]
    public void Redact_AuthLineWithAQuotedCommandWordOrAnUnreadSeparator_WithholdsAllAfterTheCommandWord(
        string line, string logged) =>
        Assert.AreEqual(logged, Redact(line));

    [TestMethod]
    [DataRow("AUTH s3cret", "AUTH ****")]
    [DataRow("auth  s3cret \t", "auth ****")]
    [DataRow("  AUTH\ts3cret", "  AUTH\t****")]
    [DataRow("AUTH PLAINs3cret", "AUTH ****")]
    [DataRow("AUTH \"s3 cret\"", "AUTH ****")]
    [DataRow("AUTH 's3 cret' ", "AUTH ****")]
    [DataRow("AUTH s3\"cr et\"", "AUTH ****")]
    [DataRow("AUTH \"s3\\\" cret\"", "AUTH ****")]
    [DataRow("AUTH 's3\\' cret'", "AUTH ****")]
    [DataRow("AUTH 's3\\\\' cr et'", "AUTH ****")]
    [DataRow("AUTH \"s3 cret", "AUTH ****")]
    [DataRow("AUTH s3cret\\", "AUTH ****")]
    [DataRow("AUTH \"s3cret\\", "AUTH ****")]
    [DataRow("AUTH \"PLAIN\"", "AUTH ****")]
    public void Redact_AuthLineWithOneArgument_WithholdsIt(string line, string logged) =>
        Assert.AreEqual(logged, Redact(line));

    [TestMethod]
    [DataRow("PLAIN")]
    [DataRow("login")]
    [DataRow("CRAM-MD5")]
    [DataRow("DIGEST-MD5")]
    [DataRow("NTLM")]
    [DataRow("GSSAPI")]
    [DataRow("GSS-SPNEGO")]
    [DataRow("EXTERNAL")]
    [DataRow("ANONYMOUS")]
    [DataRow("XOAUTH2")]
    [DataRow("OAUTHBEARER")]
    [DataRow("SCRAM-SHA-1")]
    [DataRow("SCRAM-SHA-1-PLUS")]
    [DataRow("SCRAM-SHA-256")]
    [DataRow("SCRAM-SHA-256-PLUS")]
    [DataRow("SCRAM-SHA-512")]
    [DataRow("SCRAM-SHA-512-PLUS")]
    [DataRow("TLS")]
    [DataRow("ssl")]
    [DataRow("TLS-C")]
    [DataRow("TLS-P")]
    public void Redact_AuthLineWhoseOneArgumentNamesAMechanism_IsWrittenAsReceived(string mechanism)
    {
        Assert.AreEqual("AUTH " + mechanism, Redact("AUTH " + mechanism));
        Assert.AreEqual("AUTH " + mechanism + " ", Redact("AUTH " + mechanism + " "));
    }

    [TestMethod]
    [DataRow("HELLO 3 AUTH alice s3cret", "HELLO 3 AUTH ****")]
    [DataRow("hello 2 auth alice s3cret", "hello 2 auth ****")]
    [DataRow("HELLO  3  AUTH  alice  s3cret", "HELLO  3  AUTH ****")]
    [DataRow("HELLO 3 SETNAME app AUTH alice s3cret", "HELLO 3 SETNAME app AUTH ****")]
    [DataRow("HELLO AUTH alice s3cret", "HELLO AUTH ****")]
    [DataRow("HELLO\v3\vAUTH\valice\vs3cret", "HELLO\v3\vAUTH\v****")]
    [DataRow("\"HELLO\" 3 AUTH alice s3cret", "\"HELLO\" 3 AUTH ****")]
    [DataRow("HELLO 3 'AUTH' alice s3cret", "HELLO 3 'AUTH' ****")]
    [DataRow("HELLO 3 \"\\x41UTH\" alice s3cret", "HELLO 3 \"\\x41UTH\" ****")]
    [DataRow("HELLO 3 AUTH s3cret", "HELLO 3 AUTH ****")]
    public void Redact_HelloLineWithAnAuthArgument_WithholdsAllAfterIt(string line, string logged) =>
        Assert.AreEqual(logged, Redact(line));

    [TestMethod]
    [DataRow("")]
    [DataRow(" \v ")]
    [DataRow("AUTH")]
    [DataRow("\"AUTH\" ")]
    [DataRow("AUTH\v\f\r")]
    [DataRow("AUTH alice s3cret")]
    [DataRow("AUTH \"alice\" s3cret")]
    [DataRow("EHLO\vclient")]
    [DataRow("AUTHENTICATE\vPLAIN")]
    [DataRow("\"AUTHX\" alice")]
    [DataRow("a\\b\vc")]
    [DataRow("'a\\b' c")]
    [DataRow("AUTH \"al ice\" \"a b\"")]
    [DataRow("HELLO")]
    [DataRow("HELLO 3")]
    [DataRow("HELLO 3 SETNAME app")]
    [DataRow("HELLO 3 AUTH")]
    [DataRow("HELLO 3 AUTH ")]
    [DataRow("HELLO server AUTHORS list")]
    [DataRow("HELO client.example")]
    [DataRow("EHLO AUTH alice")]
    public void Redact_AnyOtherLine_IsWrittenAsReceived(string line) =>
        Assert.AreEqual(line, Redact(line));

    [TestMethod]
    public void ObserveSent_ServerBytes_LeaveTheNextLine()
    {
        var rule = new TunnelRedisAuthLineRule();
        rule.ObserveSent(Wire.Bytes("+OK\r\n"));
        var output = new ArrayBufferWriter<byte>();
        rule.Redact(Wire.Bytes("PING"), output);

        Assert.AreEqual("PING", Wire.Text(output.WrittenSpan));
    }
}
