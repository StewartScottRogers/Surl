using System.Text;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The <c>exec</c> commands ADR-0054 decision 2 reads as SCP, and each reason it refuses one.
/// </summary>
[TestClass]
public sealed class SshScpCommandTests
{
    [TestMethod]
    [DataRow("scp -f /x", true, false, false, "/x")]
    [DataRow("scp -t /x", false, false, false, "/x")]
    [DataRow("  scp\t-pf   /x  ", true, true, false, "/x")]
    [DataRow("scp -p -t -d -v /dir", false, true, true, "/dir")]
    [DataRow("scp -dt -- -name", false, false, true, "-name")]
    [DataRow("scp -f '/a b'", true, false, false, "/a b")]
    [DataRow("scp -f \"/a \\\"b\\\" \\\\ \\$ \\` \\n\"", true, false, false, "/a \"b\" \\ $ ` \\n")]
    [DataRow("scp -f /a\\ b'c'\"d\"", true, false, false, "/a bcd")]
    [DataRow("scp -f /caf\u00e9", true, false, false, "/caf\u00e9")]
    public void Parse_AnScpCommand_ReadsItsOptionsAndPath(string command, bool isSource, bool preservesTimes, bool targetIsDirectory, string path)
    {
        var parsed = SshScpCommand.Parse(Encoding.UTF8.GetBytes(command), 8192, out var refusal);

        Assert.AreEqual(new SshScpCommand(isSource, preservesTimes, targetIsDirectory, path), parsed);
        Assert.AreEqual(string.Empty, refusal);
    }

    [TestMethod]
    [DataRow("", "not an scp command")]
    [DataRow("ls -l", "not an scp command")]
    [DataRow("scp /x", "not an scp command")]
    [DataRow("scp -ft /x", "not an scp command")]
    [DataRow("scp -f -f /x", "not an scp command")]
    [DataRow("scp -rf /x", "-r is not served")]
    [DataRow("scp -f -q /x", "unknown option -q")]
    [DataRow("scp -f", "needs exactly one path")]
    [DataRow("scp -f /x /y", "needs exactly one path")]
    [DataRow("scp -f --", "needs exactly one path")]
    [DataRow("scp -f /x;rm", "unquoted ;")]
    [DataRow("scp -f ~/x", "unquoted ~")]
    [DataRow("scp -f '/x", "unquoted '")]
    [DataRow("scp -f \"/x", "unquoted \"")]
    [DataRow("scp -f \"/x\\", "unquoted \"")]
    [DataRow("scp -f \"$HOME\"", "unquoted $")]
    [DataRow("scp -f \"`id`\"", "unquoted `")]
    [DataRow("scp -f /x\\", "unquoted \\")]
    public void Parse_ACommandSurlDoesNotServe_IsRefusedWithItsReason(string command, string reason)
    {
        var parsed = SshScpCommand.Parse(Encoding.UTF8.GetBytes(command), 8192, out var refusal);

        Assert.IsNull(parsed);
        Assert.AreEqual(reason, refusal);
    }

    [TestMethod]
    public void Parse_BytesThatAreNotUtf8_AreRefused()
    {
        var parsed = SshScpCommand.Parse([.. "scp -f /"u8, 0xFF], 8192, out var refusal);

        Assert.IsNull(parsed);
        Assert.AreEqual("not UTF-8", refusal);
    }

    [TestMethod]
    public void Parse_ACommandPastMaxLine_IsRefused()
    {
        var parsed = SshScpCommand.Parse("scp -f /xy"u8, 9, out var refusal);

        Assert.IsNull(parsed);
        Assert.AreEqual("past --max-line", refusal);
    }

    [TestMethod]
    [DataRow(10L)]
    [DataRow(0L)]
    public void Parse_ACommandWithinMaxLineOrWithNoLimit_IsRead(long maxLineBytes)
    {
        var parsed = SshScpCommand.Parse("scp -f /xy"u8, maxLineBytes, out _);

        Assert.AreEqual(new SshScpCommand(true, false, false, "/xy"), parsed);
    }
}
