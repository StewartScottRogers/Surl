namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamCurlRunResultTests
{
    private static readonly PinnedUpstreamCurlBuild Build = new(
        "win-x64", "/pinned/curl", new string('A', 64), "curl 8.21.0", ["http"], UpstreamCurlBuildRole.Reference);

    [TestMethod]
    public void Exited_ExitCodeAndOutput_KeepsThemAndIsNotTimedOut()
    {
        byte[] output = [1, 2, 3];

        var result = UpstreamCurlRunResult.Exited(Build, 22, output, "error text");

        Assert.AreSame(Build, result.Build);
        Assert.AreEqual(22, result.ExitCode);
        Assert.IsFalse(result.TimedOut);
        Assert.AreSame(output, result.StandardOutput);
        Assert.AreEqual("error text", result.StandardError);
        Assert.AreEqual($"upstream curl {Build.Sha256} exited 22", result.ToString());
    }

    [TestMethod]
    public void StoppedAtTimeout_OutputSoFar_KeepsItWithNoExitCode()
    {
        byte[] output = [4];

        var result = UpstreamCurlRunResult.StoppedAtTimeout(Build, output, "partial");

        Assert.IsNull(result.ExitCode);
        Assert.IsTrue(result.TimedOut);
        Assert.AreSame(output, result.StandardOutput);
        Assert.AreEqual("partial", result.StandardError);
        Assert.AreEqual($"upstream curl {Build.Sha256} was stopped at its timeout", result.ToString());
    }

    [TestMethod]
    public void Exited_NullBuild_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCurlRunResult.Exited(null!, 0, [], string.Empty));
    }

    [TestMethod]
    public void Exited_NullOutput_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCurlRunResult.Exited(Build, 0, null!, string.Empty));
    }

    [TestMethod]
    public void StoppedAtTimeout_NullError_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCurlRunResult.StoppedAtTimeout(Build, [], null!));
    }
}
