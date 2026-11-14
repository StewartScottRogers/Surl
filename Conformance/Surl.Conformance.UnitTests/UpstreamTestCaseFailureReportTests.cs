
namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseFailureReportTests
{
    [TestMethod]
    public void Format_FailVerdict_WritesResultsLineThenDetail()
    {
        var report = new UpstreamTestCaseFailureReport();
        report.Record(1072, "errorcode: expected 0, got 7\r\ncurl exit code: 7\r\ncurl stderr:\r\ncurl: (7) refused");

        var text = report.Format([UpstreamTestCaseResult.Pass(1), UpstreamTestCaseResult.Fail(1072, "errorcode")]);

        Assert.AreEqual("test1072 fail errorcode\nerrorcode: expected 0, got 7\ncurl exit code: 7\ncurl stderr:\ncurl: (7) refused\n\n", text);
    }

    [TestMethod]
    public void Format_Timeout_SaysCurlStopped()
    {
        var report = new UpstreamTestCaseFailureReport();
        report.Record(1452, "curl stopped after 60 s");

        var text = report.Format([UpstreamTestCaseResult.Fail(1452, UpstreamTestCaseResult.TimeoutReason)]);

        Assert.AreEqual("test1452 fail timeout\ncurl stopped after 60 s\n\n", text);
    }

    [TestMethod]
    public void Format_BlocksAreInNumberOrderAndNothingFailing_IsEmpty()
    {
        var report = new UpstreamTestCaseFailureReport();
        var ordered = report.Format([UpstreamTestCaseResult.Fail(9, "stdout"), UpstreamTestCaseResult.Fail(2, "stdout")]);
        var none = report.Format([UpstreamTestCaseResult.Pass(1), UpstreamTestCaseResult.Skip(2, "x")]);

        Assert.IsTrue(ordered.StartsWith("test2 fail stdout\nno detail recorded", StringComparison.Ordinal));
        Assert.AreEqual(string.Empty, none);
    }
}
