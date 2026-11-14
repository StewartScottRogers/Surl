namespace Surl.Protocol.Ftp;

/// <summary>
/// What <see cref="FtpUpstreamTestCaseReplies"/> accepts: the server commands the FTP server half
/// implements, and a named refusal for each it does not.
/// </summary>
[TestClass]
public sealed class FtpUpstreamTestCaseRepliesTests
{
    [TestMethod]
    public void SupportedServerCommands_AreTheOnesTheServerHalfImplements()
    {
        var supported = FtpUpstreamTestCaseReplies.SupportedServerCommands.Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(
            new[] { "COUNT", "DELAY", "NODATACONN150", "NODATACONN421", "NODATACONN425", "NOSAVE", "PASVBADIP", "REPLY", "REPLYLF", "RETRNOSIZE", "RETRSIZE", "RETRWEIRDO", "SLOWDOWN", "SLOWDOWNDATA", "STOR", "Testnum" },
            supported);
    }

    [TestMethod]
    public void Constructor_EverySupportedCommandBlankAndCommentLines_AreAccepted()
    {
        string[] lines =
        [
            "Testnum 135", "", "   ", "# a comment", "REPLY EPSV 500 no", "REPLYLF PWD 257 \"/\"", "COUNT EPSV 1",
            "RETRNOSIZE", "RETRSIZE 7", "NOSAVE", "STOR 552 disk full", "DELAY CWD 60", "SLOWDOWN", "SLOWDOWNDATA", "NODATACONN425", "NODATACONN421", "NODATACONN150", "PASVBADIP", "RETRWEIRDO",
        ];

        var replies = new FtpUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), lines);

        Assert.IsEmpty(replies.ReceivedBytes);
        Assert.IsEmpty(replies.UploadedBytes);
    }

    [TestMethod]
    [DataRow("NODATACONN")]
    [DataRow("CAPA IDLE")]
    [DataRow("AUTH PLAIN")]
    [DataRow("reply EPSV 500 no")]
    public void Constructor_ACommandTheServerHalfDoesNotImplement_IsRefusedByItsSkipReason(string line)
    {
        var word = line.Split(' ')[0];

        var exception = Assert.ThrowsExactly<NotSupportedException>(() => new FtpUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), [line]));

        Assert.Contains($"servercmd-unsupported:{word}", exception.Message);
    }

    [TestMethod]
    [DataRow("REPLY")]
    [DataRow("COUNT CWD")]
    [DataRow("COUNT CWD -1")]
    [DataRow("RETRSIZE big")]
    [DataRow("DELAY CWD")]
    [DataRow("DELAY CWD soon")]
    public void Constructor_AMalformedLine_IsAFormatException(string line)
    {
        Assert.ThrowsExactly<FormatException>(() => new FtpUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), [line]));
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new FtpUpstreamTestCaseReplies(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new FtpUpstreamTestCaseReplies(new Dictionary<string, byte[]>(), null!));
    }
}
