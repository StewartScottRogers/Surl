namespace Surl.Conformance;

[TestClass]
public sealed class CurlPortLibcurlChoiceTests
{
    private const string LibraryPath = "/curl-port/abc123/libcurl/Libcurl.dll";
    private const string RequestedPath = "/elsewhere/Libcurl.dll";

    private static readonly byte[] LibraryBytes = [0x4D, 0x5A, 0x20];
    private static readonly PinnedCurlPortClient Library = CurlPortClientLocatorTests.LibraryPin(LibraryPath, LibraryBytes);

    [TestMethod]
    public void Choose_NothingRequestedAndThePinIsPresent_ChoosesThePin()
    {
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess().WithFile(LibraryPath, LibraryBytes));

        var choice = CurlPortLibcurlChoice.Choose(locator, [Library], [], null, "win-x64");

        Assert.AreEqual(LibraryPath, choice.LibraryPath);
        Assert.AreSame(Library, choice.Pin);
        Assert.AreEqual(0, choice.ExitCode);
    }

    [TestMethod]
    public void Choose_NothingPinnedForThePlatform_IsInconclusive()
    {
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess());

        var choice = CurlPortLibcurlChoice.Choose(locator, [Library], [], null, "linux-x64");

        Assert.IsNull(choice.LibraryPath);
        Assert.IsNull(choice.Pin);
        Assert.AreEqual(PinnedLibcurlChoice.InconclusiveExitCode, choice.ExitCode);
        StringAssert.StartsWith(choice.Message, "Inconclusive: ");
    }

    [TestMethod]
    public void Choose_RequestedFileMatchesThePin_ChoosesIt()
    {
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess().WithFile(RequestedPath, LibraryBytes));

        var choice = CurlPortLibcurlChoice.Choose(locator, [Library], [], RequestedPath, "win-x64");

        Assert.AreEqual(RequestedPath, choice.LibraryPath);
        Assert.AreSame(Library, choice.Pin);
        StringAssert.Contains(choice.Message, "abc123");
    }

    [TestMethod]
    public void Choose_RequestedFileIsUnpinned_IsRefused()
    {
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess().WithFile(RequestedPath, [0x01]));

        var choice = CurlPortLibcurlChoice.Choose(locator, [Library], [], RequestedPath, "win-x64");

        Assert.IsNull(choice.LibraryPath);
        Assert.AreEqual(PinnedLibcurlChoice.NotPinnedExitCode, choice.ExitCode);
        StringAssert.Contains(choice.Message, "is not a pinned Curl port client");
    }

    [TestMethod]
    public void Choose_RequestedFileIsMissing_IsRefused()
    {
        var locator = new CurlPortClientLocator(new FakeUpstreamCurlFileAccess());

        var choice = CurlPortLibcurlChoice.Choose(locator, [Library], [], RequestedPath, "win-x64");

        Assert.AreEqual(PinnedLibcurlChoice.NotPinnedExitCode, choice.ExitCode);
        StringAssert.Contains(choice.Message, RequestedPath);
    }

    [TestMethod]
    public void Choose_NullLocator_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPortLibcurlChoice.Choose(null!, [], [], null, "win-x64"));
    }
}
