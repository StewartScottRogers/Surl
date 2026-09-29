namespace Surl.Protocol.Gopher;

[TestClass]
public sealed class GopherLogTextTests
{
    [TestMethod]
    public void Render_PrintableAscii_IsItself()
    {
        Assert.AreEqual("/a b~", GopherLogText.Render("/a b~"u8));
    }

    [TestMethod]
    public void Render_EveryOtherByte_IsEscaped()
    {
        byte[] bytes = [(byte)'\r', (byte)'\n', (byte)'\\', 0x00, 0x1B, 0x7F, 0x80, 0xFF, (byte)'\t'];

        Assert.AreEqual(@"\r\n\x5C\x00\x1B\x7F\x80\xFF\x09", GopherLogText.Render(bytes));
    }
}
