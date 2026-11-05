namespace Surl.Protocol.Http;

[TestClass]
public sealed class HttpUpstreamTestCaseServerCommandsTests
{
    [TestMethod]
    public void Parse_EverySupportedCommand_SetsItsMeaning()
    {
        var commands = HttpUpstreamTestCaseServerCommands.Parse(
            ["Testnum 1129", "# a comment", "", "  auth_required ", "idle", "no-expect", "swsclose", "writedelay: 10", "skip:\t1053700"]);

        Assert.AreEqual(new HttpUpstreamTestCaseServerCommands(true, true, true, true, TimeSpan.FromMilliseconds(10), 1053700), commands);
    }

    [TestMethod]
    public void Parse_NoLines_SetsNothing()
    {
        var commands = HttpUpstreamTestCaseServerCommands.Parse([]);

        Assert.AreEqual(new HttpUpstreamTestCaseServerCommands(false, false, false, false, TimeSpan.Zero, 0), commands);
    }

    [TestMethod]
    [DataRow("upgrade")]
    [DataRow("connection-monitor")]
    [DataRow("stream")]
    [DataRow("delay: 10")]
    [DataRow("rtp: part 1 channel 0 size 10")]
    [DataRow("REPLY EPSV 500 no")]
    public void Parse_ACommandTheServerHalfDoesNotImplement_ThrowsNamingItsSkipReason(string line)
    {
        var failure = Assert.ThrowsExactly<NotSupportedException>(() => HttpUpstreamTestCaseServerCommands.Parse([line]));

        StringAssert.Contains(failure.Message, "servercmd-unsupported:" + line.Split(' ')[0]);
        Assert.IsFalse(HttpUpstreamTestCaseServerCommands.Supported.Contains(line.Split(' ')[0]));
    }

    [TestMethod]
    [DataRow("writedelay:")]
    [DataRow("skip: -1")]
    [DataRow("skip: many")]
    public void Parse_ANumberedCommandWithoutANumber_ThrowsFormatException(string line)
    {
        Assert.ThrowsExactly<FormatException>(() => HttpUpstreamTestCaseServerCommands.Parse([line]));
    }

    [TestMethod]
    public void Supported_NamesTheSevenCommandsParseAccepts()
    {
        var supported = HttpUpstreamTestCaseServerCommands.Supported.Order(StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(new[] { "Testnum", "auth_required", "idle", "no-expect", "skip:", "swsclose", "writedelay:" }, supported);
        CollectionAssert.AreEqual(supported, HttpUpstreamTestCaseReplies.SupportedServerCommands.Order(StringComparer.Ordinal).ToArray());
    }
}
