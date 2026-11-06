namespace Surl.Mcp;

[TestClass]
public sealed class PathConfinementTests
{
    private static readonly string FileSystemRoot = Path.GetPathRoot(Path.GetTempPath())!;
    private static readonly string Root = Path.Combine(FileSystemRoot, "srv", "root");
    private static readonly string Outside = Path.Combine(FileSystemRoot, "srv", "outside");

    [TestMethod]
    public void FindRefusal_NoRoot_RefusesEveryPath()
    {
        var confinement = new PathConfinement(null, () => Root, _ => null);

        var refusal = confinement.FindRefusal("--cert", "cert.pem");

        Assert.AreEqual("--cert names a local path, and this host was started with no root (--root or SURL_MCP_ROOT)", refusal);
    }

    [TestMethod]
    [DataRow("cert.pem")]
    [DataRow("keys/./cert.pem")]
    [DataRow("keys/../cert.pem")]
    [DataRow(".")]
    public void FindRefusal_ARelativePathInsideTheRoot_IsAccepted(string path)
    {
        var confinement = new PathConfinement(Root, () => Root, _ => null);

        Assert.IsNull(confinement.FindRefusal("--cert", path));
    }

    [TestMethod]
    public void FindRefusal_AnAbsolutePathInsideTheRoot_IsAccepted()
    {
        var confinement = new PathConfinement(Root, () => Outside, _ => null);

        Assert.IsNull(confinement.FindRefusal("--cert", Path.Combine(Root, "cert.pem")));
    }

    [TestMethod]
    [DataRow("../outside/cert.pem")]
    [DataRow("../root-sibling/cert.pem")]
    public void FindRefusal_ARelativePathThatLeavesTheRoot_IsRefused(string path)
    {
        var confinement = new PathConfinement(Root, () => Root, _ => null);

        Assert.AreEqual($"--cert names {path}, which is not inside the root", confinement.FindRefusal("--cert", path));
    }

    [TestMethod]
    public void FindRefusal_ARelativePathWhileTheCurrentDirectoryIsOutsideTheRoot_IsRefused()
    {
        var confinement = new PathConfinement(Root, () => Outside, _ => null);

        Assert.IsNotNull(confinement.FindRefusal("--cert", "cert.pem"));
    }

    [TestMethod]
    public void FindRefusal_MoreParentsThanTheFileSystemRootHas_StaysAtTheFileSystemRoot()
    {
        var confinement = new PathConfinement(FileSystemRoot, () => FileSystemRoot, _ => null);

        Assert.IsNull(confinement.FindRefusal("--cert", Path.Combine("..", "..", "cert.pem")));
    }

    [TestMethod]
    public void FindRefusal_ALinkInsideTheRootToOutside_IsRefused()
    {
        var links = new Dictionary<string, string> { [Path.Combine(Root, "escape")] = Outside };
        var confinement = new PathConfinement(Root, () => Root, links.GetValueOrDefault);

        Assert.IsNotNull(confinement.FindRefusal("--cert", Path.Combine("escape", "cert.pem")));
    }

    [TestMethod]
    public void FindRefusal_ALinkFollowedByParent_AppliesTheParentToTheLinksTarget()
    {
        var links = new Dictionary<string, string> { [Path.Combine(Root, "deep")] = Path.Combine(Outside, "a") };
        var confinement = new PathConfinement(Root, () => Root, links.GetValueOrDefault);

        Assert.IsNotNull(confinement.FindRefusal("--cert", Path.Combine("deep", "..", "cert.pem")));
    }

    [TestMethod]
    public void FindRefusal_ARelativeLinkThatStaysInsideTheRoot_IsAccepted()
    {
        var links = new Dictionary<string, string> { [Path.Combine(Root, "current")] = Path.Combine("releases", "v2") };
        var confinement = new PathConfinement(Root, () => Root, links.GetValueOrDefault);

        Assert.IsNull(confinement.FindRefusal("--cert", Path.Combine("current", "cert.pem")));
    }

    [TestMethod]
    public void FindRefusal_ARootReachedThroughALink_ComparesTheResolvedPaths()
    {
        var realRoot = Path.Combine(FileSystemRoot, "private", "root");
        var links = new Dictionary<string, string> { [Path.Combine(FileSystemRoot, "srv")] = Path.Combine(FileSystemRoot, "private") };
        var confinement = new PathConfinement(Root, () => FileSystemRoot, links.GetValueOrDefault);

        Assert.IsNull(confinement.FindRefusal("--cert", Path.Combine(realRoot, "cert.pem")));
    }

    [TestMethod]
    public void FindRefusal_LinksThatLoop_IsRefused()
    {
        var a = Path.Combine(Root, "a");
        var b = Path.Combine(Root, "b");
        var links = new Dictionary<string, string> { [a] = b, [b] = a };
        var confinement = new PathConfinement(Root, () => Root, links.GetValueOrDefault);

        Assert.IsNotNull(confinement.FindRefusal("--cert", Path.Combine("a", "cert.pem")));
    }

    [TestMethod]
    public void FindRefusal_ARootWhoseLinksLoop_IsRefused()
    {
        var links = new Dictionary<string, string> { [Root] = Root };
        var confinement = new PathConfinement(Root, () => Root, links.GetValueOrDefault);

        Assert.IsNotNull(confinement.FindRefusal("--cert", Path.Combine(Root, "cert.pem")));
    }

    [TestMethod]
    public void FindRefusal_OnDiskWithARootAndAPathInsideIt_IsAccepted()
    {
        var root = AppContext.BaseDirectory;
        var confinement = new PathConfinement(root);

        Assert.IsNull(confinement.FindRefusal("--cert", Path.Combine(root, "Surl.Mcp.UnitTests.dll")));
    }

    [TestMethod]
    public void FindRefusal_OnDiskWithARelativePath_TakesItFromTheCurrentDirectory()
    {
        var confinement = new PathConfinement(Environment.CurrentDirectory);

        Assert.IsNull(confinement.FindRefusal("--cert", "cert.pem"));
    }

    [TestMethod]
    public void ReadLinkTargetOnDisk_ADirectoryAFileAndNothing_AreNotLinks()
    {
        Assert.IsNull(PathConfinement.ReadLinkTargetOnDisk(AppContext.BaseDirectory));
        Assert.IsNull(PathConfinement.ReadLinkTargetOnDisk(typeof(PathConfinementTests).Assembly.Location));
        Assert.IsNull(PathConfinement.ReadLinkTargetOnDisk(Path.Combine(AppContext.BaseDirectory, "no-such-file")));
    }
}
