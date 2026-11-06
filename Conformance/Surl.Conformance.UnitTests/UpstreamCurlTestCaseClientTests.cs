namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamCurlTestCaseClientTests
{
    [TestMethod]
    public void ExecutablePathAndDescription_ReferenceBuild_NameTheBuildsPathAndPin()
    {
        var build = UpstreamTestCaseSelectorTests.ReferencePins()[0];
        var runner = new UpstreamCurlRunner(UpstreamCurlLocation.Found(build), TimeSpan.FromSeconds(1), TimeProvider.System);

        var client = new UpstreamCurlTestCaseClient(runner);

        Assert.AreEqual(build.DefaultPath, client.ExecutablePath);
        Assert.AreEqual($"upstream curl {build.Sha256} ({build.Version})", client.Description);
    }

    [TestMethod]
    public void Constructor_NullRunner_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new UpstreamCurlTestCaseClient(null!));
    }
}
