namespace Surl.Protocol.Socks;

[TestClass]
public sealed class SocksUpstreamTestCaseRepliesTests
{
    [TestMethod]
    public void Constructor_NullLines_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SocksUpstreamTestCaseReplies(null!));
    }

    [TestMethod]
    public void Constructor_NoCommands_ConnectsEveryRequestWithoutALogin()
    {
        var replies = new SocksUpstreamTestCaseReplies([]);

        Assert.IsNull(replies.Response);
        Assert.IsFalse(replies.RequiresLogin);
        Assert.IsNull(replies.User);
        Assert.IsNull(replies.Password);
        Assert.IsNull(replies.BackendPort);
        Assert.IsNull(replies.Accounts);
    }

    [TestMethod]
    public void Constructor_EveryCommand_IsRead()
    {
        var replies = new SocksUpstreamTestCaseReplies(
            ["Testnum 717", "# a comment", "", "response 91", "method 2", "user uz3r", "password p4ssworm", "backendport 8080"]);

        Assert.AreEqual(91, replies.Response);
        Assert.IsTrue(replies.RequiresLogin);
        Assert.AreEqual("uz3r", replies.User);
        Assert.AreEqual("p4ssworm", replies.Password);
        Assert.AreEqual(8080, replies.BackendPort);
        Assert.IsNotNull(replies.Accounts);
    }

    [TestMethod]
    public void Constructor_UnknownCommand_NamesItAsUnsupported()
    {
        var exception = Assert.ThrowsExactly<NotSupportedException>(() => new SocksUpstreamTestCaseReplies(["reply 1"]));

        StringAssert.Contains(exception.Message, "servercmd-unsupported:reply");
    }

    [TestMethod]
    [DataRow("method 5")]
    [DataRow("method")]
    [DataRow("response")]
    [DataRow("response x")]
    [DataRow("backendport -1")]
    public void Constructor_ValueTheServerHalfLacks_NamesTheCommandAsUnsupported(string line)
    {
        var exception = Assert.ThrowsExactly<NotSupportedException>(() => new SocksUpstreamTestCaseReplies([line]));

        StringAssert.Contains(exception.Message, "servercmd-unsupported:" + line.Split(' ')[0]);
    }

    [TestMethod]
    public void SupportedServerCommands_AreTheWordsSocksdReads()
    {
        CollectionAssert.AreEquivalent(
            new[] { "Testnum", "response", "method", "user", "password", "backendport" },
            SocksUpstreamTestCaseReplies.SupportedServerCommands.ToArray());
    }
}
