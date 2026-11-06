namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortClientLocatorTests
{
    private const string ClientPath = "/curl-port/abc123/curl";
    private const string OtherClientPath = "/curl-port/def456/curl";
    private const string UpstreamPath = "/pinned/reference/curl";

    private static readonly byte[] ClientBytes = [0x4D, 0x5A, 0x10];
    private static readonly byte[] OtherClientBytes = [0x4D, 0x5A, 0x11];
    private static readonly byte[] UpstreamBytes = [0x4D, 0x5A, 0x01];

    [TestMethod]
    public void Locate_PinnedFileMatches_ReturnsTheClient()
    {
        var pin = Pin(ClientPath, ClientBytes);
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess().WithFile(ClientPath, ClientBytes));

        var location = locator.Locate([pin], [], "win-x64");

        Assert.IsTrue(location.IsAvailable);
        Assert.AreSame(pin, location.Client);
        Assert.AreEqual(ClientPath, location.Path);
        Assert.AreEqual(CurlPortClientUnavailability.None, location.Unavailability);
        StringAssert.Contains(location.Message, ClientPath);
        StringAssert.Contains(location.Message, "abc123");
    }

    [TestMethod]
    public void Locate_PinInLowerCaseHex_ComparesCaseInsensitively()
    {
        var pin = Pin(ClientPath, ClientBytes) with { Sha256 = FakeUpstreamCurlFileAccess.Sha256Of(ClientBytes).ToLowerInvariant() };
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess().WithFile(ClientPath, ClientBytes));

        var location = locator.Locate([pin], [], "win-x64");

        Assert.AreSame(pin, location.Client);
    }

    [TestMethod]
    public void Locate_Sha256Differs_ThrowsUnpinnedCurlPortClientException()
    {
        var pin = Pin(ClientPath, ClientBytes);
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess().WithFile(ClientPath, OtherClientBytes));

        var exception = Assert.ThrowsExactly<UnpinnedCurlPortClientException>(() => locator.Locate([pin], [], "win-x64"));

        Assert.AreEqual(ClientPath, exception.Path);
        Assert.AreEqual(FakeUpstreamCurlFileAccess.Sha256Of(OtherClientBytes), exception.Sha256);
    }

    [TestMethod]
    public void Locate_FirstPinRefusedAndSecondMatches_ReturnsTheSecond()
    {
        var refused = Pin(ClientPath, ClientBytes);
        var matching = Pin(OtherClientPath, OtherClientBytes);
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess()
            .WithFile(ClientPath, UpstreamBytes)
            .WithFile(OtherClientPath, OtherClientBytes));

        var location = locator.Locate([refused, matching], [], "win-x64");

        Assert.AreSame(matching, location.Client);
    }

    [TestMethod]
    public void Locate_PinnedPathIsListedInUpstreamCurlBuilds_NeverReturnsIt()
    {
        var pin = Pin(UpstreamPath, UpstreamBytes);
        var upstream = UpstreamPin(UpstreamPath.ToUpperInvariant(), UpstreamBytes);
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess().WithFile(UpstreamPath, UpstreamBytes));

        var location = locator.Locate([pin], [upstream], "win-x64");

        Assert.IsFalse(location.IsAvailable);
        Assert.IsNull(location.Path);
        Assert.AreEqual(CurlPortClientUnavailability.NoPinnedClientForPlatform, location.Unavailability);
    }

    [TestMethod]
    public void Locate_FileHashesToAnUpstreamCurlPin_ThrowsUnpinnedCurlPortClientException()
    {
        var pin = Pin(ClientPath, UpstreamBytes);
        var upstream = UpstreamPin(UpstreamPath, UpstreamBytes);
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess().WithFile(ClientPath, UpstreamBytes));

        var exception = Assert.ThrowsExactly<UnpinnedCurlPortClientException>(() => locator.Locate([pin], [upstream], "win-x64"));

        Assert.AreEqual(ClientPath, exception.Path);
    }

    [TestMethod]
    public void Locate_NoPinForPlatform_ReportsNoPinnedClient()
    {
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess().WithFile(ClientPath, ClientBytes));

        var location = locator.Locate([Pin(ClientPath, ClientBytes)], [], "linux-x64");

        Assert.IsFalse(location.IsAvailable);
        Assert.IsNull(location.Client);
        Assert.AreEqual(CurlPortClientUnavailability.NoPinnedClientForPlatform, location.Unavailability);
        StringAssert.Contains(location.Message, "linux-x64");
        StringAssert.Contains(location.Message, CurlPortClientPins.FileName);
    }

    [TestMethod]
    public void Locate_PinnedFileAbsent_ReportsTheFileAbsentWithHowToPublishIt()
    {
        var pin = Pin(ClientPath, ClientBytes);
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess());

        var location = locator.Locate([pin], [], "win-x64");

        Assert.IsFalse(location.IsAvailable);
        Assert.IsNull(location.Path);
        Assert.AreEqual(CurlPortClientUnavailability.PinnedClientFileAbsent, location.Unavailability);
        StringAssert.Contains(location.Message, ClientPath);
        StringAssert.Contains(location.Message, pin.PublishCommand);
    }

    [TestMethod]
    public void RunnerFor_PinnedFileAbsent_MarksTheTestInconclusive()
    {
        var location = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess()).Locate([Pin(ClientPath, ClientBytes)], [], "win-x64");

        var exception = Assert.ThrowsExactly<AssertInconclusiveException>(() => PinnedCurlPortClientOnThisMachine.RunnerFor(location));

        StringAssert.Contains(exception.Message, ClientPath);
    }

    [TestMethod]
    public void RunnerFor_ClientLocated_ReturnsARunnerForIt()
    {
        var pin = Pin(ClientPath, ClientBytes);

        var runner = PinnedCurlPortClientOnThisMachine.RunnerFor(CurlPortClientLocation.Found(pin, ClientPath));

        Assert.AreSame(pin, runner.Client);
    }

    [TestMethod]
    public void Locate_PathNamesAnEnvironmentVariable_ExpandsIt()
    {
        var expanded = Environment.ExpandEnvironmentVariables("%PATH%") + "/curl";
        var pin = Pin("%PATH%/curl", ClientBytes);
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess().WithFile(expanded, ClientBytes));

        var location = locator.Locate([pin], [], "win-x64");

        Assert.AreEqual(expanded, location.Path);
    }

    [TestMethod]
    public void Locate_NullArguments_ThrowArgumentNullException()
    {
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess());

        Assert.ThrowsExactly<ArgumentNullException>(() => locator.Locate(null!, [], "win-x64"));
        Assert.ThrowsExactly<ArgumentNullException>(() => locator.Locate([], null!, "win-x64"));
        Assert.ThrowsExactly<ArgumentNullException>(() => locator.Locate([], [], null!));
    }

    internal static PinnedCurlPortClient Pin(string path, byte[] contents, string platform = "win-x64") =>
        new(platform, path, FakeUpstreamCurlFileAccess.Sha256Of(contents), "abc123", "dotnet publish Curl.Console -c Release -r win-x64", "2026-11-04");

    private static PinnedUpstreamCurlBuild UpstreamPin(string path, byte[] contents) =>
        new("win-x64", path, FakeUpstreamCurlFileAccess.Sha256Of(contents), "curl 8.21.0", [], UpstreamCurlBuildRole.Reference);
}
