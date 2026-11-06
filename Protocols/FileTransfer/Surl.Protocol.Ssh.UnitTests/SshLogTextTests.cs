namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshLogTextTests
{
    [TestMethod]
    public void Render_EveryKindOfByte_IsReversibleAndFreeOfControlCharacters()
    {
        byte[] bytes = [(byte)'A', (byte)' ', (byte)'~', (byte)'\r', (byte)'\n', (byte)'\\', 0x00, 0x1B, 0x7F, 0xFF];

        var text = SshLogText.Render(bytes);

        Assert.AreEqual(@"A ~\r\n\x5C\x00\x1B\x7F\xFF", text);
    }

    [TestMethod]
    public void RenderNameList_Names_AreJoinedByCommasAndEscaped()
    {
        var text = SshLogText.RenderNameList(["aes128-ctr", "bé\u0007"]);

        Assert.AreEqual(@"aes128-ctr,b\xE9\x07", text);
    }
}
