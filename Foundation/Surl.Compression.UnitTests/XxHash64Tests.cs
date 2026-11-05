using System.Text;

namespace Surl.Compression;

/// <summary>
/// XXH64 with seed 0 against published vectors (xxHash's own and its Python binding's
/// documentation), with one longer input, 13 bytes, so an 8-byte word, a 4-byte word and a byte all
/// follow the stripes: its value is Microsoft's independent <c>System.IO.Hashing.XxHash64</c>'s,
/// read 2026-11-05, which also gives each published vector.
/// </summary>
[TestClass]
public sealed class XxHash64Tests
{
    [TestMethod]
    [DataRow("", 0xEF46DB3751D8E999UL)]
    [DataRow("a", 0xD24EC4F1A98C6E5BUL)]
    [DataRow("abc", 0x44BC2CF5AD770999UL)]
    [DataRow("Nobody inspects the spammish repetition", 0xFBCEA83C8A378BF1UL)]
    [DataRow("abcdefghijklm", 0x934ADBC0EBC51325UL)]
    public void Hash_PublishedVector_MatchesIt(string text, ulong expected)
    {
        Assert.AreEqual(expected, XxHash64.Hash(Encoding.ASCII.GetBytes(text)));
    }
}
