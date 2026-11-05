using System.Text;

namespace Surl.Conformance;

[TestClass]
public sealed class UpstreamTestCaseFileServersTests
{
    [TestMethod]
    public void HostKeyHashes_AreInTheFormsCurlsHostPubOptionsTake()
    {
        using var servers = new UpstreamTestCaseFileServers();

        Assert.MatchesRegex("^[0-9a-f]{32}$", servers.HostKeyMd5);
        Assert.MatchesRegex("^[A-Za-z0-9+/]{43}$", servers.HostKeySha256);
        Assert.AreEqual("ssh-rsa", servers.HostKeyAlgorithm);
    }

    [TestMethod]
    public void ClientKeyFiles_AreAPkcs1PrivateKeyAndItsOpenSshPublicKeyLine()
    {
        using var servers = new UpstreamTestCaseFileServers();

        var privateKey = Encoding.ASCII.GetString(servers.ClientKeyFiles["server/curl_client_key"]);
        var publicKey = Encoding.ASCII.GetString(servers.ClientKeyFiles["server/curl_client_key.pub"]);

        Assert.StartsWith("-----BEGIN RSA PRIVATE KEY-----", privateKey);
        Assert.StartsWith("ssh-rsa AAAA", publicKey);
    }

    [TestMethod]
    public void CreateSsh_ServesScpAndSftp()
    {
        using var servers = new UpstreamTestCaseFileServers();

        var ssh = servers.CreateSsh(Path.GetTempPath(), "runner");

        CollectionAssert.IsSubsetOf(new[] { "scp", "sftp" }, ssh.Schemes.ToArray());
    }

    [TestMethod]
    [DataRow("runner", "runner")]
    [DataRow("First Last", "FirstLast")]
    [DataRow("a.b_c-d:e", "a.b_c-de")]
    [DataRow("  ", "surl")]
    public void SshLoginNameFor_UserName_KeepsOnlyCharactersAnUnquotedLoginSurvives(string userName, string expected)
    {
        var name = UpstreamTestCaseFileServers.SshLoginNameFor(userName);

        Assert.AreEqual(expected, name);
    }

    [TestMethod]
    public void CreateSmb_ServesSmb()
    {
        var smb = UpstreamTestCaseFileServers.CreateSmb(Path.GetTempPath());

        CollectionAssert.Contains(smb.Schemes.ToArray(), "smb");
    }
}
