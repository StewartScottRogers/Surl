namespace Surl.Protocol.Ftp;

/// <summary>
/// How <see cref="FtpUpstreamTestCaseServerCommands"/> reads a <c>REPLY</c>'s response, as a
/// Perl double-quoted string (FILEFORMAT.md at tag <c>curl-8_21_0</c>).
/// </summary>
[TestClass]
public sealed class FtpUpstreamTestCaseServerCommandsTests
{
    [TestMethod]
    [DataRow("plain text", "plain text", DisplayName = "No escape")]
    [DataRow("a\\r\\nb\\tc", "a\r\nb\tc", DisplayName = "CR, LF and tab")]
    [DataRow("\\x41\\x4", "A\u0004", DisplayName = "Two and one hexadecimal digits")]
    [DataRow("\\x414", "A4", DisplayName = "At most two hexadecimal digits")]
    [DataRow("a\\xZ", "a\0Z", DisplayName = "No hexadecimal digit is the character 0")]
    [DataRow("a\\x", "a\0", DisplayName = "No hexadecimal digit at the end")]
    [DataRow("\\\\ \\\" \\$", "\\ \" $", DisplayName = "A backslash before any other character is that character")]
    [DataRow("ends\\", "ends\\", DisplayName = "A backslash at the end stays")]
    public void ReadPerlDoubleQuoted_ReadsTheEscapes(string text, string expected)
    {
        var read = FtpUpstreamTestCaseServerCommands.ReadPerlDoubleQuoted(text);

        Assert.AreEqual(expected, read);
    }

    [TestMethod]
    public void TakeReply_ACommandWithNoReply_IsNull()
    {
        var commands = FtpUpstreamTestCaseServerCommands.Parse(["REPLY EPSV 500 no"]);

        Assert.IsNull(commands.TakeReply("PASV"));
        Assert.AreEqual("500 no\r\n", commands.TakeReply("EPSV"));
        Assert.AreEqual("500 no\r\n", commands.TakeReply("EPSV"), "Without COUNT a REPLY lasts.");
    }

    [TestMethod]
    public void TakeReply_ReplyWithNoResponse_IsAnEmptyLine()
    {
        var commands = FtpUpstreamTestCaseServerCommands.Parse(["REPLY EPSV"]);

        Assert.AreEqual("\r\n", commands.TakeReply("EPSV"));
    }
}
