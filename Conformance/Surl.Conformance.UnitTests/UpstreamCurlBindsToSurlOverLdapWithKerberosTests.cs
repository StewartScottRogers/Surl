using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Windows reference build - upstream curl 8.21.0 over <c>WinLDAP</c> - binds to a
/// live, in-process <c>surl ldap://... --keytab</c> with Kerberos inside <c>GSS-SPNEGO</c> and
/// searches it over the sealed RFC 4121 layer (ADR-0072 Amendment 1), its ticket issued by the
/// hand-built test KDC on <c>127.0.0.1:88</c> for <c>tester@SURL.TEST</c>. The URL names
/// <c>localhost</c>, for which <c>WinLDAP</c> asks for <c>ldap/&lt;the machine's host name&gt;:&lt;port&gt;</c>,
/// so the port is chosen before the KDC starts and surl listens on it. Inconclusive off Windows,
/// without BL-265's <c>SURL.TEST</c> realm mapping, with port 88 taken, or where the pinned build
/// is not installed (ADR-0065 decision 3).
/// </summary>
[TestClass]
[TestCategory("Integration")]
[OSCondition(OperatingSystems.Windows)]
[DoNotParallelize]
public sealed class UpstreamCurlBindsToSurlOverLdapWithKerberosTests
{
    private const string AliceDn = "cn=alice,dc=example,dc=com";

    private static readonly string Credentials = $"{KerberosTestKdcOnLoopback.UserPrincipal}:{KerberosTestKdcOnLoopback.Password}";

    private string? accountsDirectory;

    public TestContext TestContext { get; set; } = null!;

    [TestCleanup]
    public void DeleteAccountsDirectory()
    {
        if (accountsDirectory is not null)
        {
            Directory.Delete(accountsDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task NegotiateBind_KerberosAccount_Exits0WithTheEntryThroughTheSealedLayer()
    {
        var port = FreeLoopbackPort();
        await using var kdc = await KerberosTestKdcOnLoopback.StartAsync(
            [$"ldap/{Dns.GetHostName()}:{port}"], TestContext.CancellationToken);
        var accounts = await WriteKerberosAccountsFileAsync();
        await using var surl = await SurlOnLoopback.StartOnPortAsync(
            "ldap",
            port,
            LdapConformanceDirectory.Files,
            LdapConformanceDirectory.Subdirectories,
            ["--auth", "negotiate", "--keytab", kdc.KeytabPath, "--user-file", accounts],
            TestContext.CancellationToken);

        var result = await PinnedUpstreamCurl.RunReferenceForProtocolAsync(
            TestContext,
            "ldap",
            ["-sS", "-m", "20", "--negotiate", "-u", Credentials, $"ldap://localhost:{port}/{AliceDn}?cn?base"]);

        Assert.AreEqual(0, result.ExitCode, $"{result.StandardError}\nsurl: {surl.Log}");
        Assert.AreEqual($"DN: {AliceDn}\n\tcn: alice\n\n", Encoding.UTF8.GetString(result.StandardOutput));
    }

    // A port the system just handed out and released; surl binds it next, so the KDC can name it
    // in the service principal before surl starts.
    private static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    // A --user-file whose one account is the KDC's user principal; its password is never used.
    private async Task<string> WriteKerberosAccountsFileAsync()
    {
        accountsDirectory = Directory.CreateTempSubdirectory("surl-conformance-ldap-kerberos-").FullName;
        var path = Path.Combine(accountsDirectory, "users.txt");
        await File.WriteAllTextAsync(path, $"{KerberosTestKdcOnLoopback.UserPrincipal}:unused\n", TestContext.CancellationToken);
        return path;
    }
}
