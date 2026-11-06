namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCasePortRowTests
{
    [TestMethod]
    public void Verdict_UpstreamFailed_IsSurlDefectWhateverThePortDid()
    {
        var row = new UpstreamTestCasePortRow(1, "http", UpstreamTestCaseResult.Fail(1, "protocol"), UpstreamTestCaseResult.Pass(1));

        Assert.AreEqual(CurlPortVerdictKind.SurlDefect, row.Verdict);
    }

    [TestMethod]
    public void Verdict_UpstreamSkipped_IsSurlDefect()
    {
        var row = new UpstreamTestCasePortRow(1, "http", UpstreamTestCaseResult.Skip(1, "x"), UpstreamTestCaseResult.Pass(1));

        Assert.AreEqual(CurlPortVerdictKind.SurlDefect, row.Verdict);
    }

    [TestMethod]
    public void Verdict_BothPassed_IsAgreement()
    {
        var row = new UpstreamTestCasePortRow(1, "ftp", UpstreamTestCaseResult.Pass(1), UpstreamTestCaseResult.Pass(1));

        Assert.AreEqual(CurlPortVerdictKind.Agreement, row.Verdict);
        Assert.AreEqual("test1 (ftp): upstream pass, port pass: Agreement", row.ToString());
    }

    [TestMethod]
    public void Verdict_PortSkipped_IsNotComparable()
    {
        var row = new UpstreamTestCasePortRow(1451, "smb", UpstreamTestCaseResult.Pass(1451), UpstreamTestCaseResult.Skip(1451, "protocol-missing:smb"));

        Assert.AreEqual(CurlPortVerdictKind.NotComparable, row.Verdict);
        Assert.AreEqual("test1451 (smb): upstream pass, port skip protocol-missing:smb: NotComparable", row.ToString());
    }

    [TestMethod]
    public void Verdict_PortFailed_IsPortDefect()
    {
        var row = new UpstreamTestCasePortRow(838, "mail", UpstreamTestCaseResult.Pass(838), UpstreamTestCaseResult.Fail(838, "timeout"));

        Assert.AreEqual(CurlPortVerdictKind.PortDefect, row.Verdict);
        Assert.AreEqual("test838", row.Name);
    }
}
