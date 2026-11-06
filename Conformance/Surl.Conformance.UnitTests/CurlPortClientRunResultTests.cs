namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortClientRunResultTests
{
    private static readonly PinnedCurlPortClient Client = CurlPortClientLocatorTests.Pin("/curl-port/abc123/curl", [0x4D, 0x5A, 0x10]);

    [TestMethod]
    public void Exited_KeepsTheExitCodeAndOutput()
    {
        var result = CurlPortClientRunResult.Exited(Client, 7, [0x41], "error");

        Assert.AreSame(Client, result.Client);
        Assert.AreEqual(7, result.ExitCode);
        Assert.IsFalse(result.TimedOut);
        CollectionAssert.AreEqual(new byte[] { 0x41 }, result.StandardOutput);
        Assert.AreEqual("error", result.StandardError);
        Assert.AreEqual($"Curl port client abc123 ({Client.Sha256}) exited 7", result.ToString());
    }

    [TestMethod]
    public void StoppedAtTimeout_HasNoExitCode()
    {
        var result = CurlPortClientRunResult.StoppedAtTimeout(Client, [], string.Empty);

        Assert.IsNull(result.ExitCode);
        Assert.IsTrue(result.TimedOut);
        Assert.AreEqual($"Curl port client abc123 ({Client.Sha256}) was stopped at its timeout", result.ToString());
    }

    [TestMethod]
    public void Exited_NullArguments_ThrowArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortClientRunResult.Exited(null!, 0, [], string.Empty));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortClientRunResult.Exited(Client, 0, null!, string.Empty));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortClientRunResult.Exited(Client, 0, [], null!));
    }
}
