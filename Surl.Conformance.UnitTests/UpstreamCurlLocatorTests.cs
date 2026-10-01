using System.Runtime.InteropServices;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamCurlLocatorTests
{
    private const string ReferencePath = "/pinned/reference/curl";
    private const string SupplementaryPath = "/pinned/supplementary/curl";
    private const string LibraryPath = "/pinned/reference/libcurl-4.dll";

    private static readonly byte[] ReferenceBytes = [0x4D, 0x5A, 0x01];
    private static readonly byte[] SupplementaryBytes = [0x4D, 0x5A, 0x02];
    private static readonly byte[] PortBytes = [0x4D, 0x5A, 0x03];
    private static readonly byte[] LibraryBytes = [0x4D, 0x5A, 0x04];

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
    public void LocateForProtocol_ReferenceSupportsProtocol_ReturnsTheReferenceBuild()
    {
        var supplementary = Pin(SupplementaryPath, SupplementaryBytes, UpstreamCurlBuildRole.Supplementary, ["http", "smb"]);
        var reference = Pin(ReferencePath, ReferenceBytes, protocols: ["http"]);
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess()
            .WithFile(SupplementaryPath, SupplementaryBytes)
            .WithFile(ReferencePath, ReferenceBytes));

        var location = locator.LocateForProtocol([supplementary, reference], "win-x64", "http");

        Assert.AreSame(reference, location.Build);
    }

    [TestMethod]
    public void LocateForProtocol_OnlySupplementarySupportsProtocol_ReturnsTheFirstSupplementaryThatDoes()
    {
        var reference = Pin(ReferencePath, ReferenceBytes, protocols: ["http"]);
        var withoutSmb = Pin("/pinned/http2/curl", PortBytes, UpstreamCurlBuildRole.Supplementary, ["http"]);
        var withSmb = Pin(SupplementaryPath, SupplementaryBytes, UpstreamCurlBuildRole.Supplementary, ["http", "smb"]);
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess()
            .WithFile(ReferencePath, ReferenceBytes)
            .WithFile("/pinned/http2/curl", PortBytes)
            .WithFile(SupplementaryPath, SupplementaryBytes));

        var location = locator.LocateForProtocol([reference, withoutSmb, withSmb], "win-x64", "SMB");

        Assert.AreSame(withSmb, location.Build);
    }

    [TestMethod]
    public void LocateForProtocol_FirstSupportingSupplementaryAbsent_ReturnsTheNextThatVerifies()
    {
        var absent = Pin("/absent/curl", SupplementaryBytes, UpstreamCurlBuildRole.Supplementary, ["smb"]);
        var present = Pin(SupplementaryPath, SupplementaryBytes, UpstreamCurlBuildRole.Supplementary, ["smb"]);
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(SupplementaryPath, SupplementaryBytes));

        var location = locator.LocateForProtocol([absent, present], "win-x64", "smb");

        Assert.AreSame(present, location.Build);
    }

    [TestMethod]
    public void LocateForProtocol_ReferenceSupportsProtocolButIsAbsent_ReportsFileAbsentRatherThanFallingBack()
    {
        var reference = Pin(ReferencePath, ReferenceBytes, protocols: ["http"]);
        var supplementary = Pin(SupplementaryPath, SupplementaryBytes, UpstreamCurlBuildRole.Supplementary, ["http"]);
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(SupplementaryPath, SupplementaryBytes));

        var location = locator.LocateForProtocol([reference, supplementary], "win-x64", "http");

        Assert.AreEqual(UpstreamCurlUnavailability.PinnedBuildFileAbsent, location.Unavailability);
        StringAssert.Contains(location.Message, ReferencePath);
    }

    [TestMethod]
    public void LocateForProtocol_NoBuildOfPlatformSupportsProtocol_ReportsNoPinnedBuildAndThrowsNothing()
    {
        var reference = Pin(ReferencePath, ReferenceBytes, protocols: ["http"]);
        var otherPlatform = Pin(SupplementaryPath, SupplementaryBytes, protocols: ["smb"]) with { Platform = "linux-x64" };
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess()
            .WithFile(ReferencePath, ReferenceBytes)
            .WithFile(SupplementaryPath, SupplementaryBytes));

        var location = locator.LocateForProtocol([reference, otherPlatform], "win-x64", "smb");

        Assert.IsFalse(location.IsAvailable);
        Assert.AreEqual(UpstreamCurlUnavailability.NoPinnedBuildForPlatform, location.Unavailability);
        Assert.AreEqual("UpstreamCurlBuilds.json pins no upstream curl build for win-x64 that supports smb.", location.Message);
    }

    [TestMethod]
    [DataRow("smb", @"C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe")]
    [DataRow("smbs", @"C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe")]
    [DataRow("http", @"C:\Program Files\Git\mingw64\bin\curl.exe")]
    public void LocateForProtocol_RealPinFileOnWindows_SelectsTheBuildItsAdrNames(string protocol, string expectedPath)
    {
        var fileAccess = new FakeUpstreamCurlFileAccess();
        var pins = RealPinsWithEveryFilePresent(fileAccess);
        var locator = new UpstreamCurlLocator(fileAccess);

        var location = locator.LocateForProtocol(pins, "win-x64", protocol);

        Assert.AreEqual(expectedPath, location.Build?.DefaultPath);
    }

    [TestMethod]
    [DataRow("ldap", "/opt/upstream-curl/8.21.0-openldap/curl")]
    [DataRow("ldaps", "/opt/upstream-curl/8.21.0-openldap/curl")]
    [DataRow("http", "/opt/upstream-curl/8.21.0/curl")]
    public void LocateForProtocol_RealPinFileOnLinux_SelectsTheBuildItsAdrNames(string protocol, string expectedPath)
    {
        var fileAccess = new FakeUpstreamCurlFileAccess();
        var pins = RealPinsWithEveryFilePresent(fileAccess, "linux-x64");
        var locator = new UpstreamCurlLocator(fileAccess);

        var location = locator.LocateForProtocol(pins, "linux-x64", protocol);

        Assert.AreEqual(expectedPath, location.Build?.DefaultPath);
    }

    [TestMethod]
    public void Locate_RealPinFileOnLinux_StillReturnsTheReferenceBuild()
    {
        var fileAccess = new FakeUpstreamCurlFileAccess();
        var pins = RealPinsWithEveryFilePresent(fileAccess, "linux-x64");
        var locator = new UpstreamCurlLocator(fileAccess);

        var location = locator.Locate(pins, "linux-x64");

        Assert.AreEqual("/opt/upstream-curl/8.21.0/curl", location.Build?.DefaultPath);
    }

    [TestMethod]
    public void LocateForProtocol_NullPins_ThrowsArgumentNullException()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        Assert.ThrowsExactly<ArgumentNullException>(() => locator.LocateForProtocol(null!, "win-x64", "smb"));
    }

    [TestMethod]
    public void LocateForProtocol_NullPlatform_ThrowsArgumentNullException()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        Assert.ThrowsExactly<ArgumentNullException>(() => locator.LocateForProtocol([], null!, "smb"));
    }

    [TestMethod]
    public void LocateForProtocol_NullProtocol_ThrowsArgumentNullException()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        Assert.ThrowsExactly<ArgumentNullException>(() => locator.LocateForProtocol([], "win-x64", null!));
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
    public void RequirePinned_FileIsThePinnedLibrary_RefusesToRunIt()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(LibraryPath, LibraryBytes));

        var exception = Assert.ThrowsExactly<UnpinnedUpstreamCurlException>(
            () => locator.RequirePinned([LibraryPin()], LibraryPath));

        Assert.AreEqual(UpstreamCurlBuildKind.Curl, exception.Kind);
    }

    [TestMethod]
    public void RequirePinnedLibrary_FileMatchesTheLibraryPin_ReturnsThatPin()
    {
        var library = LibraryPin();
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile("/copy/libcurl-4.dll", LibraryBytes));

        var build = locator.RequirePinnedLibrary([Pin(ReferencePath, ReferenceBytes), library], "/copy/libcurl-4.dll");

        Assert.AreSame(library, build);
    }

    [TestMethod]
    public void RequirePinnedLibrary_FileIsAPinnedCurl_RefusesToLoadIt()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(ReferencePath, ReferenceBytes));

        var exception = Assert.ThrowsExactly<UnpinnedUpstreamCurlException>(
            () => locator.RequirePinnedLibrary([Pin(ReferencePath, ReferenceBytes), LibraryPin()], ReferencePath));

        Assert.AreEqual(UpstreamCurlBuildKind.Library, exception.Kind);
        StringAssert.Contains(exception.Message, "is not a pinned upstream libcurl");
    }

    [TestMethod]
    public void RequirePinnedLibrary_FileHashDiffers_RefusesWithItsHash()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(LibraryPath, PortBytes));

        var exception = Assert.ThrowsExactly<UnpinnedUpstreamCurlException>(
            () => locator.RequirePinnedLibrary([LibraryPin()], LibraryPath));

        Assert.AreEqual(FakeUpstreamCurlFileAccess.Sha256Of(PortBytes), exception.Sha256);
    }

    [TestMethod]
    public void RequirePinnedLibrary_FileAbsent_ThrowsFileNotFoundException()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        var exception = Assert.ThrowsExactly<FileNotFoundException>(
            () => locator.RequirePinnedLibrary([LibraryPin()], "/missing/libcurl-4.dll"));

        Assert.AreEqual("The libcurl to load, /missing/libcurl-4.dll, was not found.", exception.Message);
    }

    [TestMethod]
    public void LocateLibrary_PinnedFileMatches_ReturnsTheLibrary()
    {
        var library = LibraryPin();
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess()
            .WithFile(ReferencePath, ReferenceBytes)
            .WithFile(LibraryPath, LibraryBytes));

        var location = locator.LocateLibrary([Pin(ReferencePath, ReferenceBytes), library], "win-x64");

        Assert.AreSame(library, location.Build);
        StringAssert.Contains(location.Message, "upstream libcurl");
    }

    [TestMethod]
    public void LocateLibrary_NoLibraryPinnedForThePlatform_ReportsNoPinnedLibrary()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(LibraryPath, LibraryBytes));

        var location = locator.LocateLibrary([Pin(ReferencePath, ReferenceBytes), LibraryPin()], "linux-x64");

        Assert.IsFalse(location.IsAvailable);
        Assert.AreEqual("UpstreamCurlBuilds.json pins no upstream libcurl for linux-x64.", location.Message);
    }

    [TestMethod]
    public void LocateLibrary_FileAbsent_ReportsItAbsent()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        var location = locator.LocateLibrary([LibraryPin()], "win-x64");

        Assert.AreEqual(UpstreamCurlUnavailability.PinnedBuildFileAbsent, location.Unavailability);
    }

    [TestMethod]
    public void LocateLibrary_FileHashDiffers_RefusesAsALibrary()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(LibraryPath, PortBytes));

        var exception = Assert.ThrowsExactly<UnpinnedUpstreamCurlException>(() => locator.LocateLibrary([LibraryPin()], "win-x64"));

        Assert.AreEqual(UpstreamCurlBuildKind.Library, exception.Kind);
    }

    [TestMethod]
    public void LocateLibrary_NullArguments_ThrowArgumentNullException()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess());

        Assert.ThrowsExactly<ArgumentNullException>(() => locator.LocateLibrary(null!, "win-x64"));
        Assert.ThrowsExactly<ArgumentNullException>(() => locator.LocateLibrary([], null!));
    }

    [TestMethod]
    public void Locate_OnlyALibraryPinned_ReportsNoPinnedBuild()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(LibraryPath, LibraryBytes));

        var location = locator.Locate([LibraryPin()], "win-x64");

        Assert.AreEqual(UpstreamCurlUnavailability.NoPinnedBuildForPlatform, location.Unavailability);
    }

    [TestMethod]
    public void LocateForProtocol_LibrarySupportsTheProtocol_NeverReturnsTheLibrary()
    {
        var locator = new UpstreamCurlLocator(new FakeUpstreamCurlFileAccess().WithFile(LibraryPath, LibraryBytes));

        var location = locator.LocateForProtocol([LibraryPin()], "win-x64", "ws");

        Assert.AreEqual(UpstreamCurlUnavailability.NoPinnedBuildForPlatform, location.Unavailability);
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
        UpstreamCurlBuildRole role = UpstreamCurlBuildRole.Reference,
        string[]? protocols = null) =>
        new("win-x64", path, FakeUpstreamCurlFileAccess.Sha256Of(contents), "curl 8.21.0", protocols ?? ["http"], role);

    private static PinnedUpstreamCurlBuild LibraryPin() =>
        new("win-x64", LibraryPath, FakeUpstreamCurlFileAccess.Sha256Of(LibraryBytes), "libcurl/8.21.0", ["ws", "wss"], UpstreamCurlBuildRole.Reference, UpstreamCurlBuildKind.Library);

    /// <summary>
    /// Reads the repository's real <c>UpstreamCurlBuilds.json</c> and puts a distinct stand-in
    /// file at every pin's default path, re-pinning each to its stand-in's hash, so selection
    /// follows the real file's platforms, roles, protocols and order without any curl on disk.
    /// Given a <paramref name="platform"/>, only that platform's pins are kept: the Linux and
    /// macOS reference builds share one default path, so one stand-in cannot serve both.
    /// </summary>
    private static List<PinnedUpstreamCurlBuild> RealPinsWithEveryFilePresent(FakeUpstreamCurlFileAccess fileAccess, string? platform = null)
    {
        var pins = UpstreamCurlBuildPins.Parse(
            File.ReadAllText(Path.Combine(PinnedUpstreamCurl.RepositoryRoot(), UpstreamCurlBuildPins.FileName)))
            .Where(pin => platform is null || pin.Platform == platform);

        return pins.Select((pin, index) =>
        {
            byte[] standIn = [0x4D, 0x5A, (byte)index];
            fileAccess.WithFile(pin.DefaultPath, standIn);
            return pin with { Sha256 = FakeUpstreamCurlFileAccess.Sha256Of(standIn) };
        }).ToList();
    }
}
