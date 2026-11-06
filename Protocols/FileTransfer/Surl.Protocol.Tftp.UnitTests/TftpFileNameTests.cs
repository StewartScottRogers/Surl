using System.Text;

namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class TftpFileNameTests
{
    [TestMethod]
    [DataRow("file.txt", "/file.txt")]
    [DataRow("/file.txt", "/file.txt")]
    [DataRow("//file.txt", "//file.txt")]
    [DataRow("", "/")]
    [DataRow("sub/a b%c.txt", "/sub/a%20b%25c.txt")]
    [DataRow("..", "/..")]
    public void ToRequestPath_AsciiName_EncodesPercentAndSpaceAndDropsOneLeadingSlash(string fileName, string requestPath)
    {
        var path = TftpFileName.ToRequestPath(Encoding.ASCII.GetBytes(fileName));

        Assert.AreEqual(requestPath, path);
    }

    [TestMethod]
    public void ToRequestPath_BytesOutsidePrintableAscii_ArePercentEncodedOneByOne()
    {
        var path = TftpFileName.ToRequestPath([0xC3, 0xA9, 0x7F, 0x00]);

        Assert.AreEqual("/%C3%A9%7F%00", path);
    }
}
