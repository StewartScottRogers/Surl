using System.Text;

namespace Surl.Protocol.Ftp;

[TestClass]
public sealed class FtpPathTests
{
    [TestMethod]
    [DataRow("", "dir", "/dir")]
    [DataRow("dir", "sub", "/dir/sub")]
    [DataRow("dir", "/other", "/other")]
    [DataRow("dir", "/", "/")]
    [DataRow("dir/sub", "..", "/dir")]
    [DataRow("dir/sub", "../../x", "/x")]
    [DataRow("dir", ".//sub/.", "/dir/sub")]
    public void Resolve_ValidPath_ResolvesLexically(string current, string argument, string expected)
    {
        var resolved = FtpPath.Resolve(Segments(current), Encoding.UTF8.GetBytes(argument));

        Assert.AreEqual(expected, "/" + string.Join('/', resolved!));
    }

    [TestMethod]
    [DataRow("", "..")]
    [DataRow("dir", "../..")]
    [DataRow("dir", "/../dir")]
    public void Resolve_ClimbingAboveTheRoot_HasNoResolution(string current, string argument)
    {
        Assert.IsNull(FtpPath.Resolve(Segments(current), Encoding.UTF8.GetBytes(argument)));
    }

    [TestMethod]
    public void Resolve_NotUtf8_HasNoResolution()
    {
        Assert.IsNull(FtpPath.Resolve([], [0xC3, 0x28]));
    }

    [TestMethod]
    public void ToRequestPath_PercentEncodesEachSegment()
    {
        Assert.AreEqual("/", FtpPath.ToRequestPath([]));
        Assert.AreEqual("/a%25b/caf%C3%A9/x%3Fy", FtpPath.ToRequestPath(["a%b", "café", "x?y"]));
    }

    [TestMethod]
    public void ToQuotedReplyText_DoublesQuotesAndEscapesAllButPrintableAscii()
    {
        Assert.AreEqual("/", FtpPath.ToQuotedReplyText([]));
        Assert.AreEqual("/say \"\"hi\"\"/caf\\xC3\\xA9/a\\x5Cb/t\\x09", FtpPath.ToQuotedReplyText(["say \"hi\"", "café", "a\\b", "t\t"]));
    }

    [TestMethod]
    public void ToReplyText_KeepsQuotesAndEscapesAllButPrintableAscii()
    {
        Assert.AreEqual("say \"hi\"/caf\\xC3\\xA9/a\\x5Cb/t\\x09", FtpPath.ToReplyText(Encoding.UTF8.GetBytes("say \"hi\"/café/a\\b/t\t")));
    }

    [TestMethod]
    public void Parent_IsTheDirectoryAboveAndTheRootForTheRoot()
    {
        Assert.IsEmpty(FtpPath.Parent([]));
        CollectionAssert.AreEqual(new[] { "dir" }, FtpPath.Parent(["dir", "sub"]).ToArray());
    }

    private static string[] Segments(string path) => path.Length == 0 ? [] : path.Split('/');
}
