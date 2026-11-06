using System.Text;

namespace Surl.Compression;

/// <summary>
/// XXH32 against published vectors: xxHash's own for seed 0, and the header checksum bytes of
/// LZ4 frames as the lz4 tool writes them (the second byte of XXH32 over FLG and BD).
/// </summary>
[TestClass]
public sealed class XxHash32Tests
{
    [TestMethod]
    [DataRow("", 0x02CC5D05U)]
    [DataRow("a", 0x550D7456U)]
    [DataRow("abc", 0x32D153FFU)]
    [DataRow("Nobody inspects the spammish repetition", 0xE2293B2FU)]
    public void Hash_PublishedVector_MatchesIt(string text, uint expected)
    {
        Assert.AreEqual(expected, XxHash32.Hash(Encoding.ASCII.GetBytes(text)));
    }

    [TestMethod]
    [DataRow((byte)0x64, (byte)0x40, (byte)0xA7)]
    [DataRow((byte)0x60, (byte)0x40, (byte)0x82)]
    public void Hash_Lz4FrameDescriptor_GivesTheHeaderChecksumByteTheLz4ToolWrites(byte flags, byte blockDescriptor, byte headerChecksum)
    {
        Assert.AreEqual(headerChecksum, (byte)(XxHash32.Hash([flags, blockDescriptor]) >> 8));
    }
}
