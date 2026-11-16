namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamLibtestTestCaseClientTests
{
    private static UpstreamLibtestTestCaseClient Client(string program = "lib2402")
    {
        var build = new PinnedUpstreamCurlBuild("linux-x64", "/pinned/libtest/libtests", "ABCD", string.Empty, [], UpstreamCurlBuildRole.Supplementary);
        var runner = new UpstreamCurlRunner(UpstreamCurlLocation.Found(build), TimeSpan.FromSeconds(1), TimeProvider.System);

        return new UpstreamLibtestTestCaseClient(runner, program);
    }

    [TestMethod]
    public void ArgumentsFor_PutsTheProgramNameFirst()
    {
        CollectionAssert.AreEqual(new[] { "lib2402", "http://h/1", "-x" }, Client().ArgumentsFor(["http://h/1", "-x"]).ToArray());
    }

    [TestMethod]
    public void ArgumentsFor_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Client().ArgumentsFor(null!));
    }

    [TestMethod]
    public void ExecutablePathAndDescription_NameTheBinaryAndTheProgram()
    {
        var client = Client("lib2405");

        Assert.AreEqual("/pinned/libtest/libtests", client.ExecutablePath);
        StringAssert.Contains(client.Description, "lib2405");
        StringAssert.Contains(client.Description, "ABCD");
    }

    [TestMethod]
    public void Constructor_NullRunnerOrBlankProgram_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new UpstreamLibtestTestCaseClient(null!, "lib1"));
        Assert.ThrowsExactly<ArgumentException>(() => Client(" "));
    }
}
