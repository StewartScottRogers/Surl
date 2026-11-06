namespace Surl.Protocol.Telnet;

[TestClass]
public sealed class TelnetLineAssemblerTests
{
    [TestMethod]
    public void Take_CrLf_CompletesTheLineWithoutItsEnding()
    {
        var lines = new TelnetLineAssembler(8192);

        var statuses = "ab\r\n"u8.ToArray().Select(lines.Take).ToArray();

        CollectionAssert.AreEqual(
            new[] { TelnetLineStatus.PartWay, TelnetLineStatus.PartWay, TelnetLineStatus.PartWay, TelnetLineStatus.Completed },
            statuses);
        CollectionAssert.AreEqual("ab"u8.ToArray(), lines.CompletedLine);
    }

    [TestMethod]
    public void Take_LineOfExactlyTheLimit_Completes()
    {
        var lines = new TelnetLineAssembler(3);

        var statuses = "ab\n"u8.ToArray().Select(lines.Take).ToArray();

        Assert.AreEqual(TelnetLineStatus.Completed, statuses[^1]);
    }

    [TestMethod]
    public void Take_LineOverTheLimit_IsTooLong()
    {
        var lines = new TelnetLineAssembler(3);

        var statuses = "abc"u8.ToArray().Select(lines.Take).ToArray();

        Assert.AreEqual(TelnetLineStatus.TooLong, statuses[^1]);
    }
}
