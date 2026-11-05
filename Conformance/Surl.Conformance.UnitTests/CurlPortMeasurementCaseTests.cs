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
}
