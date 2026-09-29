using System.Runtime.InteropServices;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamCurlLocatorTests
{
    private const string ReferencePath = "/pinned/reference/curl";
    private const string SupplementaryPath = "/pinned/supplementary/curl";

    private static readonly byte[] ReferenceBytes = [0x4D, 0x5A, 0x01];
    private static readonly byte[] SupplementaryBytes = [0x4D, 0x5A, 0x02];
    private static readonly byte[] PortBytes = [0x4D, 0x5A, 0x03];

    [TestMethod]
    public void Locate_PinnedFileMatches_ReturnsTheBuild()
    {
        var pin = Pin(ReferencePath, ReferenceBytes);
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(ReferencePath, ReferenceBytes));

        var location = locator.Locate([pin], "win-x64");

        Assert.IsTrue(location.IsAvailable);
        Assert.AreSame(pin, location.Build);
        Assert.AreEqual(UpstreamCurlUnavailability.None, location.Unavailability);
        StringAssert.Contains(location.Message, ReferencePath);
    }

    [TestMethod]
    public void Locate_PinInLowerCaseHex_ComparesCaseInsensitively()
    {
        var pin = Pin(ReferencePath, ReferenceBytes) with { Sha256 = FakeUpstreamCurlFileAccess.Sha256Of(ReferenceBytes).ToLowerInvariant() };
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(ReferencePath, ReferenceBytes));

        var location = locator.Locate([pin], "win-x64");

        Assert.AreSame(pin, location.Build);
    }

    [TestMethod]
    public void Locate_DefaultRole_NeverReturnsASupplementaryBuild()
    {
        var supplementary = Pin(SupplementaryPath, SupplementaryBytes, UpstreamCurlBuildRole.Supplementary);
        var reference = Pin(ReferencePath, ReferenceBytes);
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess()
            .WithFile(SupplementaryPath, SupplementaryBytes)
            .WithFile(ReferencePath, ReferenceBytes));

        var location = locator.Locate([supplementary, reference], "win-x64");

        Assert.AreSame(reference, location.Build);
    }

    [TestMethod]
    public void Locate_DefaultRoleAndOnlySupplementaryPinned_ReportsNoPinnedBuild()
    {
        var supplementary = Pin(SupplementaryPath, SupplementaryBytes, UpstreamCurlBuildRole.Supplementary);
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(SupplementaryPath, SupplementaryBytes));

        var location = locator.Locate([supplementary], "win-x64");

        Assert.IsFalse(location.IsAvailable);
        Assert.AreEqual(UpstreamCurlUnavailability.NoPinnedBuildForPlatform, location.Unavailability);
    }

    [TestMethod]
    public void Locate_SupplementaryRole_ReturnsTheSupplementaryBuild()
    {
        var supplementary = Pin(SupplementaryPath, SupplementaryBytes, UpstreamCurlBuildRole.Supplementary);
        var reference = Pin(ReferencePath, ReferenceBytes);
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess()
            .WithFile(SupplementaryPath, SupplementaryBytes)
            .WithFile(ReferencePath, ReferenceBytes));

        var location = locator.Locate([reference, supplementary], "win-x64", UpstreamCurlBuildRole.Supplementary);

        Assert.AreSame(supplementary, location.Build);
        StringAssert.Contains(location.Message, "supplementary");
    }

    [TestMethod]
    public void Locate_FirstPinnedFileAbsent_ReturnsTheNextOneThatExists()
    {
        var absent = Pin("/absent/curl", ReferenceBytes);
        var present = Pin(ReferencePath, ReferenceBytes);
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(ReferencePath, ReferenceBytes));

        var location = locator.Locate([absent, present], "win-x64");

        Assert.AreSame(present, location.Build);
    }

    [TestMethod]
    public void Locate_FirstFileMismatchesButSecondMatches_ReturnsTheSecond()
    {
        var mismatched = Pin("/other/curl", ReferenceBytes);
        var matching = Pin(ReferencePath, ReferenceBytes);
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess()
            .WithFile("/other/curl", PortBytes)
            .WithFile(ReferencePath, ReferenceBytes));

        var location = locator.Locate([mismatched, matching], "win-x64");

        Assert.AreSame(matching, location.Build);
    }

    [TestMethod]
    public void Locate_FileHashMatchesNoPin_RefusesWithPathHashAndPhrase()
    {
        var pin = Pin(ReferencePath, ReferenceBytes);
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(ReferencePath, PortBytes));

        var exception = Assert.ThrowsExactly<UnpinnedUpstreamCurlException>(() => locator.Locate([pin], "win-x64"));

        var portSha256 = FakeUpstreamCurlFileAccess.Sha256Of(PortBytes);
        Assert.AreEqual(
            $"{ReferencePath} (SHA-256 {portSha256}) is not a pinned upstream curl build. Surl is measured only against the builds in UpstreamCurlBuilds.json (ADR-0003); pinning another is a decision, and the Curl port is never one.",
            exception.Message);
        Assert.AreEqual(ReferencePath, exception.Path);
        Assert.AreEqual(portSha256, exception.Sha256);
    }

    [TestMethod]
    public void Locate_PlatformWithNoPin_ReportsNoPinnedBuildAndThrowsNothing()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(ReferencePath, ReferenceBytes));

        var location = locator.Locate([Pin(ReferencePath, ReferenceBytes)], "linux-x64");

        Assert.IsFalse(location.IsAvailable);
        Assert.IsNull(location.Build);
        Assert.AreEqual(UpstreamCurlUnavailability.NoPinnedBuildForPlatform, location.Unavailability);
        Assert.AreEqual("UpstreamCurlBuilds.json pins no reference upstream curl build for linux-x64.", location.Message);
    }

    [TestMethod]
    public void Locate_PinnedFileAbsent_ReportsFileAbsentAndThrowsNothing()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        var location = locator.Locate([Pin(ReferencePath, ReferenceBytes)], "win-x64");

        Assert.IsFalse(location.IsAvailable);
        Assert.AreEqual(UpstreamCurlUnavailability.PinnedBuildFileAbsent, location.Unavailability);
        Assert.AreEqual($"The pinned reference upstream curl build for win-x64 is not installed: {ReferencePath} does not exist.", location.Message);
    }

    [TestMethod]
    public void Locate_NullPins_ThrowsArgumentNullException()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        Assert.ThrowsExactly<ArgumentNullException>(() => locator.Locate(null!, "win-x64"));
    }

    [TestMethod]
    public void Locate_NullPlatform_ThrowsArgumentNullException()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        Assert.ThrowsExactly<ArgumentNullException>(() => locator.Locate([], null!));
    }

    [TestMethod]
    public void RequirePinned_FileMatchesAnyPin_ReturnsThatPin()
    {
        var reference = Pin(ReferencePath, ReferenceBytes);
        var supplementary = Pin(SupplementaryPath, SupplementaryBytes, UpstreamCurlBuildRole.Supplementary);
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile("/usr/bin/curl", SupplementaryBytes));

        var build = locator.RequirePinned([reference, supplementary], "/usr/bin/curl");

        Assert.AreSame(supplementary, build);
    }

    [TestMethod]
    public void RequirePinned_FileMatchesNoPin_RefusesWithPathHashAndPhrase()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile("/port/curl", PortBytes));

        var exception = Assert.ThrowsExactly<UnpinnedUpstreamCurlException>(
            () => locator.RequirePinned([Pin(ReferencePath, ReferenceBytes)], "/port/curl"));

        StringAssert.Contains(exception.Message, "/port/curl");
        StringAssert.Contains(exception.Message, FakeUpstreamCurlFileAccess.Sha256Of(PortBytes));
        StringAssert.Contains(exception.Message, "is not a pinned upstream curl build");
    }

    [TestMethod]
    public void RequirePinned_FileAbsent_ThrowsFileNotFoundException()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        var exception = Assert.ThrowsExactly<FileNotFoundException>(
            () => locator.RequirePinned([Pin(ReferencePath, ReferenceBytes)], "/missing/curl"));

        Assert.AreEqual("The curl to run, /missing/curl, was not found.", exception.Message);
    }

    [TestMethod]
    public void RequirePinned_NullPins_ThrowsArgumentNullException()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        Assert.ThrowsExactly<ArgumentNullException>(() => locator.RequirePinned(null!, "/usr/bin/curl"));
    }

    [TestMethod]
    public void RequirePinned_NullPath_ThrowsArgumentNullException()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        Assert.ThrowsExactly<ArgumentNullException>(() => locator.RequirePinned([], null!));
    }

    [TestMethod]
    public void CurrentPlatform_Always_NamesTheRunningSystemAndArchitecture()
    {
        var platform = UpstreamCurlLocator.CurrentPlatform;

        Assert.AreEqual(
            UpstreamCurlLocator.PlatformName(RuntimeInformation.IsOSPlatform, RuntimeInformation.OSArchitecture),
            platform);
        Assert.EndsWith("-" + RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(), platform);
    }

    [TestMethod]
    [DataRow("WINDOWS", Architecture.X64, "win-x64")]
    [DataRow("OSX", Architecture.Arm64, "osx-arm64")]
    [DataRow("LINUX", Architecture.X64, "linux-x64")]
    [DataRow("FREEBSD", Architecture.X64, "unknown-x64")]
    public void PlatformName_OperatingSystemAndArchitecture_ReturnsPortableRuntimeIdentifier(
        string operatingSystem,
        Architecture architecture,
        string expected)
    {
        var running = OSPlatform.Create(operatingSystem);

        var platform = UpstreamCurlLocator.PlatformName(candidate => candidate == running, architecture);

        Assert.AreEqual(expected, platform);
    }

    private static PinnedUpstreamCurlBuild Pin(
        string path,
        byte[] contents,
        UpstreamCurlBuildRole role = UpstreamCurlBuildRole.Reference) =>
        new("win-x64", path, FakeUpstreamCurlFileAccess.Sha256Of(contents), "curl 8.21.0", ["http"], role);
}
