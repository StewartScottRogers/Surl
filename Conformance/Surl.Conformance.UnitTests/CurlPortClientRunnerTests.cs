namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortClientRunnerTests
{
    private const string ClientPath = "/curl-port/abc123/curl";

    private static readonly PinnedCurlPortClient Client = CurlPortClientLocatorTests.Pin(ClientPath, [0x4D, 0x5A, 0x10]);

    [TestMethod]
    public void Constructor_LocationHoldsNoClient_ThrowsArgumentExceptionWithItsMessage()
    {
        var location = CurlPortClientLocation.NoPinnedClient("win-x64");

        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new CurlPortClientRunner(location, TimeSpan.FromSeconds(1), TimeProvider.System));

        StringAssert.Contains(exception.Message, location.Message);
    }

    [TestMethod]
    public void Constructor_TimeoutNotPositive_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CurlPortClientRunner(Found(), TimeSpan.Zero, TimeProvider.System));
    }

    [TestMethod]
    public void Constructor_NullArguments_ThrowArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlPortClientRunner(null!, TimeSpan.FromSeconds(1), TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlPortClientRunner(Found(), TimeSpan.FromSeconds(1), null!));
    }

    [TestMethod]
    public void CreateStartInfo_Arguments_PassesEachAsOneUnquotedArgumentToTheVerifiedPath()
    {
        var runner = new CurlPortClientRunner(Found(), TimeSpan.FromSeconds(1), TimeProvider.System);
        string[] arguments = ["-s", "mqtt5://127.0.0.1:1/a b", "\"quoted\"", string.Empty];

        var startInfo = runner.CreateStartInfo(arguments, new Dictionary<string, string>());

        Assert.AreSame(Client, runner.Client);
        Assert.AreEqual(ClientPath, startInfo.FileName);
        CollectionAssert.AreEqual(arguments, startInfo.ArgumentList.ToArray());
        Assert.AreEqual(string.Empty, startInfo.Arguments);
        Assert.IsFalse(startInfo.UseShellExecute);
        Assert.IsTrue(startInfo.CreateNoWindow);
        Assert.IsTrue(startInfo.RedirectStandardInput);
        Assert.IsTrue(startInfo.RedirectStandardOutput);
        Assert.IsTrue(startInfo.RedirectStandardError);
    }

    [TestMethod]
    public void CreateStartInfo_Environment_SetsEachVariableAndKeepsTheRest()
    {
        var runner = new CurlPortClientRunner(Found(), TimeSpan.FromSeconds(1), TimeProvider.System);
        var inherited = Environment.GetEnvironmentVariable("PATH");

        var startInfo = runner.CreateStartInfo(["-s"], new Dictionary<string, string> { ["CURL_EXTENSIONS"] = "1" });

        Assert.AreEqual("1", startInfo.Environment["CURL_EXTENSIONS"]);
        Assert.AreEqual(inherited, startInfo.Environment["PATH"]);
    }

    [TestMethod]
    public void CreateStartInfo_NullOrHoldingNull_ThrowsArgumentException()
    {
        var runner = new CurlPortClientRunner(Found(), TimeSpan.FromSeconds(1), TimeProvider.System);

        Assert.ThrowsExactly<ArgumentNullException>(() => runner.CreateStartInfo(null!, new Dictionary<string, string>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => runner.CreateStartInfo(["-s"], null!));
        Assert.ThrowsExactly<ArgumentException>(() => runner.CreateStartInfo(["-s", null!], new Dictionary<string, string>()));
    }

    private static CurlPortClientLocation Found() => CurlPortClientLocation.Found(Client, ClientPath);
}
