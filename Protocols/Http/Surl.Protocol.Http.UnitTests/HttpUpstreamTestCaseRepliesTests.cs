using System.Text;
using Surl.HttpMessage;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpUpstreamTestCaseRepliesTests
{
    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpUpstreamTestCaseReplies(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), null!));
    }

    [TestMethod]
    public void Constructor_AnUnsupportedServerCommand_Throws()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => new HttpUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), ["upgrade"]));
    }

    [TestMethod]
    public void ChooseReply_ConnectAndData_PickTheirOwnPrefixedParts()
    {
        var replies = Replies(("data1", "d1"), ("connect1", "c1"));

        var data = replies.ChooseReply(Head("GET", "/10001"));
        var connect = replies.ChooseReply(Head("CONNECT", "test.10001:443"));

        Assert.AreEqual(new HttpUpstreamTestCaseReply("data1", data.Bytes, false), data);
        Assert.AreEqual("d1", Encoding.ASCII.GetString(data.Bytes!));
        Assert.AreEqual("connect1", connect.PartName);
        Assert.AreEqual("c1", Encoding.ASCII.GetString(connect.Bytes!));
    }

    [TestMethod]
    public void ChooseReply_AfterSwsbounce_GivesTheNextPartOnceAndThenWhatIsAsked()
    {
        var replies = Replies(("data", "a swsbounce"), ("data1", "b swsbounce"), ("data2", "c"));

        var names = Enumerable.Range(0, 4).Select(_ => replies.ChooseReply(Head("GET", "/5")).PartName).ToArray();

        CollectionAssert.AreEqual(new[] { "data", "data1", "data2", "data" }, names);
    }

    [TestMethod]
    public void ChooseReply_AMissingPart_HasNoBytesAndKeepsTheConnection()
    {
        var replies = Replies(("data", "a"));

        var reply = replies.ChooseReply(Head("GET", "/10002"));

        Assert.AreEqual(new HttpUpstreamTestCaseReply("data2", null, false), reply);
    }

    [TestMethod]
    public void ChooseReply_SwscloseInThePartOrTheCommands_ClosesTheConnection()
    {
        var inPart = Replies(("data", "x swsclose"));
        var inCommands = new HttpUpstreamTestCaseReplies(new Dictionary<string, byte[]> { ["data"] = "x"u8.ToArray() }, ["swsclose"]);

        Assert.IsTrue(inPart.ChooseReply(Head("GET", "/1")).ClosesConnection);
        Assert.IsTrue(inCommands.ChooseReply(Head("GET", "/1")).ClosesConnection);
    }

    [TestMethod]
    public void ReceivedBytes_AreEveryRecordedByteInOrder()
    {
        var replies = Replies();

        replies.RecordReceived("GET "u8);
        replies.RecordReceived("/1"u8);

        Assert.AreEqual("GET /1", Encoding.ASCII.GetString(replies.ReceivedBytes));
    }

    private static HttpUpstreamTestCaseReplies Replies(params (string Name, string Text)[] parts) =>
        new(parts.ToDictionary(part => part.Name, part => Encoding.ASCII.GetBytes(part.Text)), []);

    private static HttpRequestHead Head(string method, string target) => new(method, target, new Version(1, 1), []);
}
