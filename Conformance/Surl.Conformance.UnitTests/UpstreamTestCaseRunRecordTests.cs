using System.Text;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseRunRecordTests
{
    [TestMethod]
    public void FromRun_ExitedRun_CarriesItsOutputAndTheServerHalfsBytes()
    {
        var build = UpstreamTestCaseSelectorTests.ReferencePins()[0];
        var run = UpstreamCurlRunResult.Exited(build, 7, [1, 2], "é");

        var record = UpstreamTestCaseRunRecord.FromRun(run, [3], [4], [5]);

        Assert.AreEqual(7, record.ExitCode);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, record.StandardOutput);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("é"), record.StandardError);
        CollectionAssert.AreEqual(new byte[] { 3 }, record.OutputFile);
        CollectionAssert.AreEqual(new byte[] { 4 }, record.ServerReceived);
        CollectionAssert.AreEqual(new byte[] { 5 }, record.UploadReceived);
        Assert.IsEmpty(record.ProxyReceived);
    }

    [TestMethod]
    public void FromRun_TimedOutRun_HasNoExitCode()
    {
        var run = UpstreamCurlRunResult.StoppedAtTimeout(UpstreamTestCaseSelectorTests.ReferencePins()[0], [], "");

        Assert.IsNull(UpstreamTestCaseRunRecord.FromRun(run, [], [], []).ExitCode);
    }

    [TestMethod]
    public void FromRun_NullRun_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseRunRecord.FromRun(null!, [], [], []));
}
