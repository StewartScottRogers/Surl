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
    public void CreateStartInfo_WorkingDirectory_RunsCurlThereAndByDefaultInThisProcesssOwn()
    {
        var runner = new UpstreamCurlRunner(UpstreamCurlLocation.Found(Build), TimeSpan.FromSeconds(1), TimeProvider.System);

        var inFolder = runner.CreateStartInfo(["-s"], new Dictionary<string, string>(), "/tmp/upstream-tests");
        var byDefault = runner.CreateStartInfo(["-s"]);

        Assert.AreEqual("/tmp/upstream-tests", inFolder.WorkingDirectory);
        Assert.AreEqual(string.Empty, byDefault.WorkingDirectory);
    }

    [TestMethod]
    public void CreateStartInfo_NullWorkingDirectory_ThrowsArgumentNullException()
    {
        var runner = new UpstreamCurlRunner(UpstreamCurlLocation.Found(Build), TimeSpan.FromSeconds(1), TimeProvider.System);

        Assert.ThrowsExactly<ArgumentNullException>(() => runner.CreateStartInfo(["-s"], new Dictionary<string, string>(), null!));
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

    [TestMethod]
    public async Task WriteStandardInputAsync_CurlReadsIt_WritesEveryByteAndCloses()
    {
        var pipe = new ClosedPipeStream(writeFailure: null, closeFailure: null);
        byte[] bytes = [1, 2, 3];

        await UpstreamCurlRunner.WriteStandardInputAsync(new StreamWriter(pipe), bytes, CancellationToken.None);

        CollectionAssert.AreEqual(bytes, pipe.Written.ToArray());
        Assert.IsTrue(pipe.CloseAttempted);
    }

    [TestMethod]
    public async Task WriteStandardInputAsync_CurlExitedBeforeReadingIt_IgnoresTheBrokenPipeAndCloses()
    {
        var pipe = new ClosedPipeStream(new IOException("Broken pipe"), closeFailure: null);

        await UpstreamCurlRunner.WriteStandardInputAsync(new StreamWriter(pipe), new byte[4096], CancellationToken.None);

        Assert.IsTrue(pipe.CloseAttempted);
    }

    [TestMethod]
    public async Task WriteStandardInputAsync_PipeBreaksOnWritingAndOnClosing_IgnoresBoth()
    {
        var pipe = new ClosedPipeStream(new IOException("Broken pipe"), new IOException("Broken pipe"));

        await UpstreamCurlRunner.WriteStandardInputAsync(new StreamWriter(pipe), new byte[4096], CancellationToken.None);

        Assert.IsTrue(pipe.CloseAttempted);
    }

    [TestMethod]
    public async Task WriteStandardInputAsync_PipeBreaksOnClosing_IgnoresIt()
    {
        var pipe = new ClosedPipeStream(writeFailure: null, new IOException("The pipe is being closed."));

        await UpstreamCurlRunner.WriteStandardInputAsync(new StreamWriter(pipe), new byte[] { 1 }, CancellationToken.None);

        Assert.IsTrue(pipe.CloseAttempted);
    }

    [TestMethod]
    public async Task WriteStandardInputAsync_WriteFailsOtherThanWithIOException_Propagates()
    {
        var pipe = new ClosedPipeStream(new InvalidOperationException("not a pipe failure"), closeFailure: null);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => UpstreamCurlRunner.WriteStandardInputAsync(new StreamWriter(pipe), new byte[] { 1 }, CancellationToken.None));
    }

    [TestMethod]
    public async Task WriteStandardInputAsync_NullStandardInput_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => UpstreamCurlRunner.WriteStandardInputAsync(null!, new byte[] { 1 }, CancellationToken.None));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunAsync_ProcessExitsWithoutReadingFourMebibytesOfStandardInput_ReturnsItsExitCode()
    {
        var dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrEmpty(dotnetHost) || !File.Exists(dotnetHost))
        {
            Assert.Inconclusive("DOTNET_HOST_PATH does not name the dotnet host, so there is no process to start.");
        }

        var exitsAtOnce = Build with { DefaultPath = dotnetHost };
        var runner = new UpstreamCurlRunner(UpstreamCurlLocation.Found(exitsAtOnce), TimeSpan.FromSeconds(60), TimeProvider.System);

        var result = await runner.RunAsync(["--version"], new byte[4 * 1024 * 1024], CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
    }
}
