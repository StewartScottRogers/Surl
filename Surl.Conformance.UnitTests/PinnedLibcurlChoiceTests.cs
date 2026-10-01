namespace Surl.Conformance;

[TestClass]
public sealed class PinnedLibcurlChoiceTests
{
    private const string LibraryPath = "/pinned/reference/libcurl-4.dll";
    private const string CurlPath = "/pinned/reference/curl";

    private static readonly byte[] LibraryBytes = [0x4D, 0x5A, 0x04];
    private static readonly byte[] CurlBytes = [0x4D, 0x5A, 0x01];
    private static readonly byte[] PortBytes = [0x4D, 0x5A, 0x03];

    [TestMethod]
    public void Choose_RequestedFileMatchesTheLibraryPin_ChoosesIt()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile("/copy/libcurl-4.dll", LibraryBytes));

        var choice = PinnedLibcurlChoice.Choose(locator, Pins(), "/copy/libcurl-4.dll", "linux-x64");

        Assert.AreEqual("/copy/libcurl-4.dll", choice.LibraryPath);
        Assert.AreEqual(0, choice.ExitCode);
        StringAssert.Contains(choice.Message, "matches the pinned libcurl for win-x64");
    }

    [TestMethod]
    public void Choose_RequestedFileHashDiffers_LoadsNothingAndExitsNotPinned()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile("/copy/libcurl-4.dll", PortBytes));

        var choice = PinnedLibcurlChoice.Choose(locator, Pins(), "/copy/libcurl-4.dll", "win-x64");

        Assert.IsNull(choice.LibraryPath);
        Assert.AreEqual(PinnedLibcurlChoice.NotPinnedExitCode, choice.ExitCode);
        StringAssert.Contains(choice.Message, FakeUpstreamCurlFileAccess.Sha256Of(PortBytes));
    }

    [TestMethod]
    public void Choose_RequestedFileIsThePinnedCurl_LoadsNothing()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(CurlPath, CurlBytes));

        var choice = PinnedLibcurlChoice.Choose(locator, Pins(), CurlPath, "win-x64");

        Assert.IsNull(choice.LibraryPath);
        Assert.AreEqual(PinnedLibcurlChoice.NotPinnedExitCode, choice.ExitCode);
    }

    [TestMethod]
    public void Choose_RequestedFileMissing_LoadsNothingAndExitsNotPinned()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        var choice = PinnedLibcurlChoice.Choose(locator, Pins(), "/missing/libcurl-4.dll", "win-x64");

        Assert.IsNull(choice.LibraryPath);
        Assert.AreEqual(PinnedLibcurlChoice.NotPinnedExitCode, choice.ExitCode);
        StringAssert.Contains(choice.Message, "was not found");
    }

    [TestMethod]
    public void Choose_NothingRequestedAndThePinnedLibraryPresent_ChoosesIt()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(LibraryPath, LibraryBytes));

        var choice = PinnedLibcurlChoice.Choose(locator, Pins(), null, "win-x64");

        Assert.AreEqual(LibraryPath, choice.LibraryPath);
        Assert.AreEqual(0, choice.ExitCode);
    }

    [TestMethod]
    public void Choose_NothingRequestedAndThePinnedLibraryAltered_LoadsNothing()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(LibraryPath, PortBytes));

        var choice = PinnedLibcurlChoice.Choose(locator, Pins(), null, "win-x64");

        Assert.IsNull(choice.LibraryPath);
        Assert.AreEqual(PinnedLibcurlChoice.NotPinnedExitCode, choice.ExitCode);
    }

    [TestMethod]
    [DataRow("linux-x64")]
    [DataRow("win-x64")]
    public void Choose_NoLibraryPinnedOrInstalled_IsInconclusive(string platform)
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        var choice = PinnedLibcurlChoice.Choose(locator, Pins(), null, platform);

        Assert.IsNull(choice.LibraryPath);
        Assert.AreEqual(PinnedLibcurlChoice.InconclusiveExitCode, choice.ExitCode);
        StringAssert.StartsWith(choice.Message, "Inconclusive: ");
    }

    private static List<PinnedUpstreamCurlBuild> Pins() =>
    [
        new("win-x64", CurlPath, FakeUpstreamCurlFileAccess.Sha256Of(CurlBytes), "8.21.0", ["rtsp"], UpstreamCurlBuildRole.Reference),
        new("win-x64", LibraryPath, FakeUpstreamCurlFileAccess.Sha256Of(LibraryBytes), "libcurl/8.21.0", ["ws", "wss"], UpstreamCurlBuildRole.Reference, UpstreamCurlBuildKind.Library),
    ];
}
