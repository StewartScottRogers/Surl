using System.Text;
using Surl.HttpMessage;

namespace Surl.Protocol.HttpProxy;

[TestClass]
public sealed class HttpProxyUpstreamTestCaseRepliesTests
{
    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpProxyUpstreamTestCaseReplies(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HttpProxyUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), null!));
    }

    [TestMethod]
    public void SupportedServerCommands_IsOnlyTestnum()
    {
        CollectionAssert.AreEquivalent(new[] { "Testnum" }, HttpProxyUpstreamTestCaseReplies.SupportedServerCommands.ToArray());
    }

    [TestMethod]
    public void Constructor_TestnumBlankAndCommentLines_AreAccepted()
    {
        var replies = new HttpProxyUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), ["Testnum 80", "", "  ", "# swsclose"]);

        Assert.IsEmpty(replies.ReceivedBytes);
    }

    [TestMethod]
    public void Constructor_AnyOtherCommand_ThrowsNamingTheSkip()
    {
        var exception = Assert.ThrowsExactly<NotSupportedException>(() => new HttpProxyUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), ["swsclose"]));

        StringAssert.Contains(exception.Message, "servercmd-unsupported:swsclose");
    }

    [TestMethod]
    public void ChooseReply_PicksTheDataPartAndTheConnectPartByMethod()
    {
        var replies = Replies(("data", "D"), ("connect", "C"), ("data2", "D2"));

        var data = replies.ChooseReply(Head("GET", "/1"));
        var second = replies.ChooseReply(Head("GET", "/12200002"));
        var connect = replies.ChooseReply(Head("CONNECT", "test.1:80"));

        Assert.AreEqual("data", data.PartName);
        Assert.AreEqual("D2", Text(second.Bytes));
        Assert.AreEqual("C", Text(connect.Bytes));
        Assert.IsFalse(data.ClosesConnection);
    }

    [TestMethod]
    public void ChooseReply_MissingPart_HasNoBytes()
    {
        var replies = Replies(("data", "D"));

        var reply = replies.ChooseReply(Head("CONNECT", "test.1:80"));

        Assert.AreEqual("connect", reply.PartName);
        Assert.IsNull(reply.Bytes);
        Assert.IsFalse(reply.ClosesConnection);
    }

    [TestMethod]
    public void ChooseReply_SwsbounceThenSwsclose_MovesToTheNextPartAndCloses()
    {
        var replies = Replies(("data", "swsbounce"), ("data1", "swsclose"));

        var first = replies.ChooseReply(Head("GET", "/5"));
        var second = replies.ChooseReply(Head("GET", "/5"));
        var third = replies.ChooseReply(Head("GET", "/5"));

        Assert.AreEqual("data", first.PartName);
        Assert.AreEqual("data1", second.PartName);
        Assert.IsTrue(second.ClosesConnection);
        Assert.AreEqual("data", third.PartName);
    }

    [TestMethod]
    public void RecordReceived_KeepsEveryByteInArrivalOrder()
    {
        var replies = Replies();

        replies.RecordReceived("abc"u8);
        replies.RecordReceived("def"u8);

        Assert.AreEqual("abcdef", Text(replies.ReceivedBytes));
    }

    private static HttpProxyUpstreamTestCaseReplies Replies(params (string Name, string Content)[] parts) =>
        new(parts.ToDictionary(part => part.Name, part => Encoding.Latin1.GetBytes(part.Content)), []);

    private static string Text(byte[]? bytes) => Encoding.Latin1.GetString(bytes!);

    private static HttpRequestHead Head(string method, string target) =>
        new(method, target, new Version(1, 1), []);
}
