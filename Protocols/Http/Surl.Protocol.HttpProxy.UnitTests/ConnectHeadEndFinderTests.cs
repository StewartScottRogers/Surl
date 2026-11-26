using System.Text;

namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class ConnectHeadEndFinderTests
{
    [TestMethod]
    [DataRow("CONNECT o:21 HTTP/1.1\r\n|Host: o\r\n|\r\n")]
    [DataRow("CONNECT o:21 HTTP/1.0\n|\n")]
    [DataRow("abcCONNECT o:21 HTTP/1.1\r\n|\r\n")]
    [DataRow("CONN|ECT o:21 HT|TP/1.1\r|\n|\r|\n")]
    [DataRow("CCONNECT o:21 HTTP/1.1\n|\r\n")]
    public void EndsConnectHead_TheEmptyLineAfterAConnectRequestLine_IsTrueOnlyThere(string pieces)
    {
        var finder = new ConnectHeadEndFinder();
        var parts = pieces.Split('|');

        var answers = parts.Select(part => finder.EndsConnectHead(Encoding.Latin1.GetBytes(part))).ToArray();

        CollectionAssert.AreEqual(parts.Select((_, index) => index == parts.Length - 1).ToArray(), answers);
    }

    [TestMethod]
    [DataRow("GET http://o/ HTTP/1.1\r\n|\r\n")]
    [DataRow("CONNECT o:21\r\n|\r\n")]
    [DataRow("HELO o HTTP/1.1\r\n|\r\n")]
    [DataRow("\r\n|\r\n")]
    [DataRow("CONNECT o:21 HTTP/1.1 x\r\n|\r\n")]
    [DataRow("CONNECT o:21 xHTTP/1.1\r\n|\r\n")]
    [DataRow("CONNECT o:21\r\n| HTTP/1.1\r\n|\r\n")]
    public void EndsConnectHead_AnEmptyLineWithNoConnectRequestLineBeforeIt_IsFalse(string pieces)
    {
        var finder = new ConnectHeadEndFinder();

        foreach (var part in pieces.Split('|'))
        {
            Assert.IsFalse(finder.EndsConnectHead(Encoding.Latin1.GetBytes(part)), part);
        }
    }

    [TestMethod]
    public void EndsConnectHead_ASecondEmptyLine_IsFalse()
    {
        var finder = new ConnectHeadEndFinder();
        finder.EndsConnectHead("CONNECT o:21 HTTP/1.1\r\n"u8);
        Assert.IsTrue(finder.EndsConnectHead("\r\n"u8));

        Assert.IsFalse(finder.EndsConnectHead("\r\n"u8));
    }

    [TestMethod]
    public void EndsConnectHead_ARequestLineABodyOfMoreThan64KiBRunsInto_StillStartsAHead()
    {
        var finder = new ConnectHeadEndFinder();
        finder.EndsConnectHead(new byte[(64 * 1024) + 1]);
        finder.EndsConnectHead("CONNECT o:21 HTTP/1.1\r\n"u8);

        Assert.IsTrue(finder.EndsConnectHead("\r\n"u8));
    }

    [TestMethod]
    public void EndsConnectHead_ALineOfNulBytesBeforeCrLf_IsNotEmpty()
    {
        var finder = new ConnectHeadEndFinder();
        finder.EndsConnectHead("CONNECT o:21 HTTP/1.1\r\n"u8);

        Assert.IsFalse(finder.EndsConnectHead("\0\r\n"u8));
    }
}
