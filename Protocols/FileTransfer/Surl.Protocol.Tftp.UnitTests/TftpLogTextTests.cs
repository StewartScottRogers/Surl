namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class TftpLogTextTests
{
    [TestMethod]
    public void Render_MixedBytes_KeepsPrintableAsciiAndEscapesEverythingElse()
    {
        byte[] bytes = [.. "a b~"u8, (byte)'\\', (byte)'\r', (byte)'\n', 0, 0xFF];

        var text = TftpLogText.Render(bytes);

        Assert.AreEqual(@"a b~\x5C\x0D\x0A\x00\xFF", text);
    }
}
