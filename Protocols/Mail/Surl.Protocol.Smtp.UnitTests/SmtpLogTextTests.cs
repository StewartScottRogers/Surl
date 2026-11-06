namespace Surl.Protocol.Smtp;

[TestClass]
public sealed class SmtpLogTextTests
{
    [TestMethod]
    public void Render_EscapesEveryByteThatIsNotPrintableAscii()
    {
        byte[] bytes = [(byte)'a', (byte)'\r', (byte)'\n', 0x5C, 0x00, 0x7F, 0xC3, (byte)'~'];

        Assert.AreEqual(@"a\r\n\x5C\x00\x7F\xC3~", SmtpLogText.Render(bytes));
    }
}
