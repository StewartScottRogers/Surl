using System.Security.Cryptography;

namespace Surl.Conformance;

[TestClass]
public sealed class LibcurlBytesTests
{
    [TestMethod]
    public void Unescape_EveryEscape_ReadsItsByte()
    {
        var bytes = LibcurlBytes.Unescape("a\\r\\n\\t\\0\\\\\\x03\\xe8\\qÿ\\");

        CollectionAssert.AreEqual(new byte[] { (byte)'a', 13, 10, 9, 0, (byte)'\\', 0x03, 0xE8, (byte)'q', 0xFF, (byte)'\\' }, bytes);
    }

    [TestMethod]
    [DataRow("\\x4")]
    [DataRow("\\xZZ")]
    public void Unescape_HexEscapeWithoutTwoDigits_Refuses(string text)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlBytes.Unescape(text));

        Assert.AreEqual("\\x needs two hexadecimal digits.", exception.Message);
    }

    [TestMethod]
    [DataRow("Ā")]
    [DataRow("\\€")]
    public void Unescape_CharacterAboveOneByte_Refuses(string text)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlBytes.Unescape(text));

        StringAssert.Contains(exception.Message, "is not a single byte");
    }

    [TestMethod]
    public void Show_PrintableAsItselfAndTheRestEscaped()
    {
        Assert.AreEqual("$\\x00\\x00\\x04a\\\\\\\"~\\x7F", LibcurlBytes.Show([(byte)'$', 0, 0, 4, (byte)'a', (byte)'\\', (byte)'"', (byte)'~', 0x7F]));
    }

    [TestMethod]
    public void Describe_UpToTheLongestShown_ShowsTheBytes()
    {
        var bytes = Enumerable.Repeat((byte)'x', LibcurlBytes.LongestShown).ToArray();

        Assert.AreEqual($"256 bytes \"{new string('x', 256)}\"", LibcurlBytes.Describe(bytes));
    }

    [TestMethod]
    public void Describe_LongerThanTheLongestShown_ShowsTheSha256()
    {
        var bytes = Enumerable.Repeat((byte)'x', LibcurlBytes.LongestShown + 1).ToArray();

        Assert.AreEqual($"257 bytes sha256 {Convert.ToHexString(SHA256.HashData(bytes))}", LibcurlBytes.Describe(bytes));
    }
}
