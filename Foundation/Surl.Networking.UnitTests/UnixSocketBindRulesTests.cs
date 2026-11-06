using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Networking;

[TestClass]
public sealed class UnixSocketBindRulesTests
{
    private static readonly Func<string, bool> NothingExists = _ => false;
    private static readonly Func<string, bool> OnlyDirectoriesExist = path => !path.EndsWith(".sock", StringComparison.Ordinal);

    [TestMethod]
    [DataRow(false, 107)]
    [DataRow(true, 103)]
    public void MaximumPathBytes_IsSunPathLessItsNul(bool isMacOs, int expected)
    {
        Assert.AreEqual(expected, UnixSocketBindRules.MaximumPathBytes(isMacOs));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    public void FindRefusalBeforeBinding_AbstractNameOffLinux_IsAbstractUnixSocketUnsupported(bool isLinux, bool isMacOs)
    {
        var refusal = UnixSocketBindRules.FindRefusalBeforeBinding(
            new UnixSocketAddress("surl-test", true), isLinux, isMacOs, NothingExists);

        Assert.AreEqual(ListenerBindFailure.AbstractUnixSocketUnsupported, refusal);
    }

    [TestMethod]
    public void FindRefusalBeforeBinding_AbstractNameOnLinux_IsNoneAndNeverLooksAtTheFileSystem()
    {
        var refusal = UnixSocketBindRules.FindRefusalBeforeBinding(
            new UnixSocketAddress("surl-test", true), true, false, _ => throw new AssertFailedException("looked"));

        Assert.IsNull(refusal);
    }

    [TestMethod]
    [DataRow(true, false, 107, null)]
    [DataRow(true, false, 108, ListenerBindFailure.UnixSocketPathTooLong)]
    [DataRow(false, false, 107, null)]
    [DataRow(false, false, 108, ListenerBindFailure.UnixSocketPathTooLong)]
    [DataRow(false, true, 103, null)]
    [DataRow(false, true, 104, ListenerBindFailure.UnixSocketPathTooLong)]
    public void FindRefusalBeforeBinding_PathLength_IsRefusedPastThePlatformsLimit(
        bool isLinux, bool isMacOs, int length, ListenerBindFailure? expected)
    {
        var path = "/" + new string('y', length - 6) + ".sock";

        var refusal = UnixSocketBindRules.FindRefusalBeforeBinding(
            new UnixSocketAddress(path, false), isLinux, isMacOs, OnlyDirectoriesExist);

        Assert.AreEqual(expected, refusal);
    }

    [TestMethod]
    public void FindRefusalBeforeBinding_PathLength_CountsUtf8Bytes()
    {
        var path = "/" + new string('é', 51) + ".sock";

        var refusal = UnixSocketBindRules.FindRefusalBeforeBinding(
            new UnixSocketAddress(path, false), true, false, OnlyDirectoriesExist);

        Assert.AreEqual(ListenerBindFailure.UnixSocketPathTooLong, refusal);
    }

    [TestMethod]
    public void FindRefusalBeforeBinding_AbstractNameTooLong_IsUnixSocketPathTooLong()
    {
        var refusal = UnixSocketBindRules.FindRefusalBeforeBinding(
            new UnixSocketAddress(new string('n', 108), true), true, false, NothingExists);

        Assert.AreEqual(ListenerBindFailure.UnixSocketPathTooLong, refusal);
    }

    [TestMethod]
    public void FindRefusalBeforeBinding_SomethingAtThePath_IsAddressInUse()
    {
        var refusal = UnixSocketBindRules.FindRefusalBeforeBinding(
            new UnixSocketAddress("/tmp/surl.sock", false), true, false, _ => true);

        Assert.AreEqual(ListenerBindFailure.AddressInUse, refusal);
    }

    [TestMethod]
    public void FindRefusalBeforeBinding_DirectoryNotThere_IsAddressNotAvailable()
    {
        var refusal = UnixSocketBindRules.FindRefusalBeforeBinding(
            new UnixSocketAddress("/tmp/surl.sock", false), true, false, NothingExists);

        Assert.AreEqual(ListenerBindFailure.AddressNotAvailable, refusal);
    }

    [TestMethod]
    public void FindRefusalBeforeBinding_FreePathInADirectoryThatIsThere_IsNone()
    {
        var refusal = UnixSocketBindRules.FindRefusalBeforeBinding(
            new UnixSocketAddress("/tmp/surl.sock", false), false, true, OnlyDirectoriesExist);

        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void FindRefusalBeforeBinding_FileSystemRoot_HasNoDirectoryToLookFor()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;

        var refusal = UnixSocketBindRules.FindRefusalBeforeBinding(
            new UnixSocketAddress(root, false), true, false, path => path != root);

        Assert.IsNull(refusal);
    }

    [TestMethod]
    [DataRow("/tmp/surl.sock", false, "/tmp/surl.sock")]
    [DataRow("surl-test", true, "\0surl-test")]
    public void EndPointNameOf_IsThePathOrTheNameBehindANul(string name, bool isAbstract, string expected)
    {
        Assert.AreEqual(expected, UnixSocketBindRules.EndPointNameOf(new UnixSocketAddress(name, isAbstract)));
    }

    [TestMethod]
    public void DescribePeer_NoEndPoint_IsTheUnnamedPeer()
    {
        Assert.AreSame(UnnamedUnixSocketPeer.Instance, UnixSocketBindRules.DescribePeer(null));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("\0\0\0")]
    [DataRow("@    ")]
    public void DescribePeer_EndPointWithNoName_IsTheUnnamedPeer(string name)
    {
        var peer = UnixSocketBindRules.DescribePeer(new NamedEndPoint(name));

        Assert.AreSame(UnnamedUnixSocketPeer.Instance, peer);
    }

    [TestMethod]
    public void DescribePeer_EndPointWhoseNameIsNull_IsTheUnnamedPeer()
    {
        var peer = UnixSocketBindRules.DescribePeer(new NamedEndPoint(null));

        Assert.AreSame(UnnamedUnixSocketPeer.Instance, peer);
    }

    [TestMethod]
    public void DescribePeer_ClientThatBoundAName_IsItsEndPoint()
    {
        var endPoint = new UnixDomainSocketEndPoint("/tmp/client.sock");

        var peer = UnixSocketBindRules.DescribePeer(endPoint);

        Assert.AreSame(endPoint, peer);
    }

    private sealed class NamedEndPoint(string? name) : EndPoint
    {
        public override AddressFamily AddressFamily => AddressFamily.Unix;

        public override string? ToString() => name;
    }
}
