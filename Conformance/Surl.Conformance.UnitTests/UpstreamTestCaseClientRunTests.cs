namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseClientRunTests
{
    [TestMethod]
    public void From_UpstreamRun_CarriesItsExitCodeOutputAndError()
    {
        var run = UpstreamCurlRunResult.Exited(UpstreamTestCaseSelectorTests.ReferencePins()[0], 7, [1, 2], "e");

        var clientRun = UpstreamTestCaseClientRun.From(run);

        Assert.AreEqual(7, clientRun.ExitCode);
        Assert.IsFalse(clientRun.TimedOut);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, clientRun.StandardOutput);
        Assert.AreEqual("e", clientRun.StandardError);
    }

    [TestMethod]
    public void From_PortRunStoppedAtItsTimeout_HasNoExitCode()
    {
        var port = CurlPortClientLocatorTests.Pin("/curl-port/curl", [0x4D]);
        var run = CurlPortClientRunResult.StoppedAtTimeout(port, [3], "late");

        var clientRun = UpstreamTestCaseClientRun.From(run);

        Assert.IsNull(clientRun.ExitCode);
        Assert.IsTrue(clientRun.TimedOut);
        CollectionAssert.AreEqual(new byte[] { 3 }, clientRun.StandardOutput);
        Assert.AreEqual("late", clientRun.StandardError);
    }

    [TestMethod]
    public void From_NullRun_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseClientRun.From((UpstreamCurlRunResult)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseClientRun.From((CurlPortClientRunResult)null!));
    }
}
