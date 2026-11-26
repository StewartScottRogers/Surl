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
