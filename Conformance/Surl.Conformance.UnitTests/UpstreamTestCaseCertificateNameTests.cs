namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseCertificateNameTests
{
    [TestMethod]
    public void Find_NullCase_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTestCaseCertificateName.Find(null!));
    }

    [TestMethod]
    public void Find_HttpsLineNamingTheSubjectAltNameCertificate_ReadsItsName()
    {
        Assert.AreEqual("test-localhost0h", UpstreamTestCaseCertificateName.Find(Case("https test-localhost0h.pem")));
    }

    [TestMethod]
    public void Find_HttpsLineNamingTheNullPrefixedCertificate_ReadsItsName()
    {
        Assert.AreEqual("test-localhost.nn", UpstreamTestCaseCertificateName.Find(Case("https test-localhost.nn.pem")));
    }

    [TestMethod]
    public void Find_PlainHttpsLine_ReadsNoName()
    {
        Assert.IsNull(UpstreamTestCaseCertificateName.Find(Case("https")));
    }

    [TestMethod]
    public void Find_HttpsLineWithAnotherWordThatIsNotAPemFile_ReadsNoName()
    {
        Assert.IsNull(UpstreamTestCaseCertificateName.Find(Case("https extra")));
    }

    [TestMethod]
    public void Find_OtherServerNamingAPemFile_ReadsNoName()
    {
        Assert.IsNull(UpstreamTestCaseCertificateName.Find(Case("http test-localhost.pem")));
    }

    [TestMethod]
    public void Find_CaseWithoutServerPart_ReadsNoName()
    {
        var testCase = UpstreamTestCaseParser.Parse(
            UpstreamTestCaseText.Bytes("<testcase>\n<client>\n<command>\nx\n</command>\n</client>\n</testcase>\n"),
            UpstreamTestCaseText.Environment());

        Assert.IsNull(UpstreamTestCaseCertificateName.Find(testCase));
    }

    [TestMethod]
    public void Find_SeveralServers_ReadsTheHttpsLinesName()
    {
        Assert.AreEqual("test-localhost-san-first", UpstreamTestCaseCertificateName.Find(Case("http\nhttps test-localhost-san-first.pem")));
    }

    private static UpstreamTestCase Case(string serverLines) =>
        UpstreamTestCaseParser.Parse(
            UpstreamTestCaseText.Bytes($"<testcase>\n<client>\n<server>\n{serverLines}\n</server>\n</client>\n</testcase>\n"),
            UpstreamTestCaseText.Environment());
}
