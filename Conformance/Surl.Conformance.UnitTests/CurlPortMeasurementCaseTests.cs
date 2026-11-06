namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortMeasurementCaseTests
{
    [TestMethod]
    public void ArgumentsFor_ReplacesThePlaceholderWithTheRunsBaseUrl()
    {
        var measurementCase = CurlPortComparisonTests.Case();

        CollectionAssert.AreEqual(
            new[] { "-s", "-S", "http://127.0.0.1:50101/hello.txt" },
            measurementCase.ArgumentsFor(new Uri("http://127.0.0.1:50101/")).ToArray());
        Assert.AreEqual("curl -s -S {surl}/hello.txt", measurementCase.CommandLine);
        Assert.ThrowsExactly<ArgumentNullException>(() => measurementCase.ArgumentsFor(null!));
    }

    [TestMethod]
    public void Defaults_CompareNoStandardErrorAndNoStoredFileAndNameNoReleaseDifference()
    {
        var measurementCase = CurlPortComparisonTests.Case();

        Assert.IsEmpty(measurementCase.AssertedStandardErrorFragments);
        Assert.IsEmpty(measurementCase.ComparedStoredFiles);
        Assert.IsNull(measurementCase.ReleaseDifference);
    }

    [TestMethod]
    [DataRow("http", true)]
    [DataRow("ftp", true)]
    [DataRow("smtp", true)]
    [DataRow("ws", true)]
    [DataRow("https", false)]
    [DataRow("sftp", false)]
    [DataRow("tftp", false)]
    public void RecordsClientBytes_ByDefault_OnlyForAPlaintextTcpScheme(string scheme, bool recorded)
    {
        var measurementCase = CurlPortComparisonTests.Case() with { SurlScheme = scheme };
        var fresh = new CurlPortMeasurementCase(
            measurementCase.Name, measurementCase.Mirrors, scheme, measurementCase.Arguments, scheme, [], measurementCase.Files, measurementCase.CheckMirroredAssertions);

        Assert.AreEqual(recorded, fresh.RecordsClientBytes);
        Assert.IsFalse((fresh with { RecordsClientBytes = false }).RecordsClientBytes);
    }
}
