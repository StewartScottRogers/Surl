namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class TftpUpstreamTestCaseRepliesTests
{
    [TestMethod]
    public void Constructor_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TftpUpstreamTestCaseReplies(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TftpUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), null!));
    }

    [TestMethod]
    public void Constructor_TestnumWriteDelayBlankAndComment_AreAccepted()
    {
        var replies = new TftpUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), ["Testnum 1238", "", "# a comment", "writedelay: 2000"]);

        Assert.AreEqual(TimeSpan.FromSeconds(2), replies.WriteDelay);
        CollectionAssert.AreEquivalent(new[] { "Testnum", "writedelay:" }, TftpUpstreamTestCaseReplies.SupportedServerCommands.ToArray());
    }

    [TestMethod]
    public void Constructor_NoWriteDelay_IsZero()
    {
        Assert.AreEqual(TimeSpan.Zero, new TftpUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), []).WriteDelay);
    }

    [TestMethod]
    public void Constructor_UnimplementedCommand_ThrowsNotSupported()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => new TftpUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), ["REPLY RRQ 500"]));
    }

    [TestMethod]
    [DataRow("writedelay:")]
    [DataRow("writedelay: soon")]
    public void Constructor_WriteDelayWithoutANumber_ThrowsFormat(string line)
    {
        Assert.ThrowsExactly<FormatException>(() => new TftpUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), [line]));
    }

    [TestMethod]
    [DataRow("/271", "data")]
    [DataRow("/20020003", "data3")]
    [DataRow("/99999999999999999999", "data")]
    [DataRow("/invalid-file", null)]
    public void FindFile_ByTheNamesLastNumber_FindsItsPart(string fileName, string? partName)
    {
        var parts = new Dictionary<string, byte[]> { ["data"] = [1], ["data3"] = [3] };
        var replies = new TftpUpstreamTestCaseReplies(parts, []);

        var file = replies.FindFile(fileName);

        Assert.AreSame(partName is null ? null : parts[partName], file);
    }
}
