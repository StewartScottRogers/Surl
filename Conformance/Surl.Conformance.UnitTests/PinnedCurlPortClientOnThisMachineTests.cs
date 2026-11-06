namespace Surl.Conformance;

/// <summary>
/// Plumbing check for the Curl port client <c>CurlPortBuilds.json</c> pins for this platform
/// (ADR-0083): the client is located and runs to an exit. It asserts nothing about the client's
/// output, which is never an expected value (ADR-0003). Inconclusive where no client is pinned
/// or installed for this platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class PinnedCurlPortClientOnThisMachineTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_PinnedClient_RunsVersionAndExitsZero()
    {
        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "--version");

        Assert.AreEqual(0, result.ExitCode, $"{result}; stderr: {result.StandardError}");
    }
}
