namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamCurlLocationTests
{
    private static readonly PinnedUpstreamCurlBuild Supplementary = new(
        "win-x64",
        "/pinned/curl",
        "0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778",
        "curl 8.22.0",
        ["smb"],
        UpstreamCurlBuildRole.Supplementary);

    [TestMethod]
    public void Found_Build_IsAvailableAndNamesPathRoleAndPlatform()
    {
        var location = UpstreamCurlLocation.Found(Supplementary);

        Assert.IsTrue(location.IsAvailable);
        Assert.AreSame(Supplementary, location.Build);
        Assert.AreEqual(UpstreamCurlUnavailability.None, location.Unavailability);
        Assert.AreEqual("/pinned/curl is the pinned supplementary upstream curl build for win-x64.", location.Message);
    }

    [TestMethod]
    public void NoPinnedBuild_PlatformAndRole_IsUnavailableAndSaysNoPin()
    {
        var location = UpstreamCurlLocation.NoPinnedBuild("osx-arm64", UpstreamCurlBuildRole.Supplementary);

        Assert.IsFalse(location.IsAvailable);
        Assert.IsNull(location.Build);
        Assert.AreEqual(UpstreamCurlUnavailability.NoPinnedBuildForPlatform, location.Unavailability);
        Assert.AreEqual("UpstreamCurlBuilds.json pins no supplementary upstream curl build for osx-arm64.", location.Message);
    }

    [TestMethod]
    public void FileAbsent_Build_IsUnavailableAndNamesTheMissingPath()
    {
        var location = UpstreamCurlLocation.FileAbsent(Supplementary);

        Assert.IsFalse(location.IsAvailable);
        Assert.IsNull(location.Build);
        Assert.AreEqual(UpstreamCurlUnavailability.PinnedBuildFileAbsent, location.Unavailability);
        Assert.AreEqual("The pinned supplementary upstream curl build for win-x64 is not installed: /pinned/curl does not exist.", location.Message);
    }
}
