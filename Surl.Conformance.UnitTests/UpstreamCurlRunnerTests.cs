namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamCurlRunnerTests
{
    private static readonly PinnedUpstreamCurlBuild Build = new(
        "win-x64", "/pinned/curl", new string('A', 64), "curl 8.21.0", ["http"], UpstreamCurlBuildRole.Reference);

    [TestMethod]
    public void Constructor_FoundLocation_RunsThatBuild()
    {
        var runner = new UpstreamCurlRunner(UpstreamCurlLocation.Found(Build), TimeSpan.FromSeconds(1), TimeProvider.System);

        Assert.AreSame(Build, runner.Build);
    }

    [TestMethod]
    public void Constructor_LocationWithNoBuild_ThrowsWithTheLocatorsReason()
    {
        var location = UpstreamCurlLocation.NoPinnedBuild("linux-x64", UpstreamCurlBuildRole.Reference);

        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new UpstreamCurlRunner(location, TimeSpan.FromSeconds(1), TimeProvider.System));

        StringAssert.StartsWith(exception.Message, location.Message);
        Assert.AreEqual("location", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_LocationHoldsALibrary_RefusesToRunIt()
    {
        var library = Build with { DefaultPath = "/pinned/libcurl-4.dll", Kind = UpstreamCurlBuildKind.Library };

        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new UpstreamCurlRunner(UpstreamCurlLocation.Found(library), TimeSpan.FromSeconds(1), TimeProvider.System));

        StringAssert.StartsWith(exception.Message, "/pinned/libcurl-4.dll is a pinned upstream libcurl, not a curl to run.");
        Assert.AreEqual("location", exception.ParamName);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Constructor_TimeoutNotPositive_ThrowsArgumentOutOfRangeException(int seconds)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new UpstreamCurlRunner(UpstreamCurlLocation.Found(Build), TimeSpan.FromSeconds(seconds), TimeProvider.System));
    }

    [TestMethod]
    public void Constructor_NullLocation_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new UpstreamCurlRunner(null!, TimeSpan.FromSeconds(1), TimeProvider.System));
    }

    [TestMethod]
    public void Constructor_NullTimeProvider_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new UpstreamCurlRunner(UpstreamCurlLocation.Found(Build), TimeSpan.FromSeconds(1), null!));
    }

    [TestMethod]
    public void CreateStartInfo_Arguments_PassesEachAsOneUnquotedArgumentToThePinnedBuild()
    {
        var runner = new UpstreamCurlRunner(UpstreamCurlLocation.Found(Build), TimeSpan.FromSeconds(1), TimeProvider.System);
        string[] arguments = ["-s", "http://127.0.0.1:1/a b.txt", "\"quoted\"", string.Empty];

        var startInfo = runner.CreateStartInfo(arguments);

        Assert.AreEqual("/pinned/curl", startInfo.FileName);
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
        var runner = new UpstreamCurlRunner(UpstreamCurlLocation.Found(Build), TimeSpan.FromSeconds(1), TimeProvider.System);
        var inherited = Environment.GetEnvironmentVariable("PATH");

        var startInfo = runner.CreateStartInfo(
            ["-s"], new Dictionary<string, string> { ["HOME"] = "/tmp/curl-home", ["USERPROFILE"] = "/tmp/curl-home" });

        Assert.AreEqual("/tmp/curl-home", startInfo.Environment["HOME"]);
        Assert.AreEqual("/tmp/curl-home", startInfo.Environment["USERPROFILE"]);
        Assert.AreEqual(inherited, startInfo.Environment["PATH"]);
    }

    [TestMethod]
    public void CreateStartInfo_NullEnvironment_ThrowsArgumentNullException()
    {
        var runner = new UpstreamCurlRunner(UpstreamCurlLocation.Found(Build), TimeSpan.FromSeconds(1), TimeProvider.System);

        Assert.ThrowsExactly<ArgumentNullException>(() => runner.CreateStartInfo(["-s"], null!));
    }

    [TestMethod]
    public void CreateStartInfo_NullArgumentList_ThrowsArgumentNullException()
    {
        var runner = new UpstreamCurlRunner(UpstreamCurlLocation.Found(Build), TimeSpan.FromSeconds(1), TimeProvider.System);

        Assert.ThrowsExactly<ArgumentNullException>(() => runner.CreateStartInfo(null!));
    }

    [TestMethod]
    public void CreateStartInfo_NullArgument_ThrowsArgumentException()
    {
        var runner = new UpstreamCurlRunner(UpstreamCurlLocation.Found(Build), TimeSpan.FromSeconds(1), TimeProvider.System);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => runner.CreateStartInfo(["-s", null!]));

        Assert.AreEqual("arguments", exception.ParamName);
    }
}
