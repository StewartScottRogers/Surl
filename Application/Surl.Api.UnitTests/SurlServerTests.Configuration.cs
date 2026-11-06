using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-616: accounts, --auth, the loosenings, certificates and host keys go through Surl.Composition's
// start, so the --user-file and --hostkey files are read through the ReadStartFile seam and each refusal is surl's.
public sealed partial class SurlServerTests
{
    private const string Http = "http://127.0.0.1:0";

    [TestMethod]
    public async Task StartAsync_AWellFormedUserFileAndAnAccount_Starts()
    {
        var seams = new ApiTestSeams();
        seams.StartFiles["users.txt"] = Encoding.UTF8.GetBytes("bob:pw\n");
        var options = ApiTestSeams.Options(Http);
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        options.UserFile = "users.txt";

        await using var server = await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken);

        Assert.AreEqual(1, seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task StartAsync_AMalformedUserFile_ThrowsFailedInitNamingTheLine()
    {
        var seams = new ApiTestSeams();
        seams.StartFiles["users.txt"] = Encoding.UTF8.GetBytes("alice:pw\nbob\n");
        var options = ApiTestSeams.Options(Http);
        options.UserFile = "users.txt";

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(SurlExitCode.FailedInit, exception.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(2) User file users.txt, line 2: expected <user:password>" }, exception.Lines.ToArray());
        Assert.AreEqual(0, seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task StartAsync_AnUnreadableUserFile_ThrowsCouldNotReadFile()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options(Http);
        options.UserFile = "missing.txt";

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exception.ExitCode);
        CollectionAssert.AreEqual(new[] { "(37) Could not read user file missing.txt" }, exception.Lines.ToArray());
    }

    [TestMethod]
    public async Task StartAsync_GssapiWithoutAKeytab_ThrowsFailedInitWithSurlsText()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options(Http);
        options.AuthenticationMethods = ["gssapi"];

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(SurlExitCode.FailedInit, exception.ExitCode);
        CollectionAssert.AreEqual(new[] { "(2) --auth gssapi needs --keytab" }, exception.Lines.ToArray());
    }

    [TestMethod]
    public async Task StartAsync_AllowAnonymous_WritesTheAllowAnonymousWarningToTheLog()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options(Http);
        options.AllowAnonymous = true;
        options.Log = new StringWriter();

        await using (await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken))
        {
        }

        Assert.Contains(
            "surl: warning: --allow-anonymous: every request and login is accepted without checking credentials",
            options.Log.ToString()!);
    }

    [TestMethod]
    public async Task StartAsync_AListenUrlAndAnAccountAlone_WritesNoLooseningWarning()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options(Http);
        options.Accounts.Add(new SurlAccount("alice", "secret"));
        options.Log = new StringWriter();

        await using (await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken))
        {
        }

        Assert.DoesNotContain("warning", options.Log.ToString()!);
    }

    // The --cert file is read by ServerTlsComposition, not through the ReadStartFile seam (BL-616 Notes),
    // so a certificate the test cannot supply is refused as surl refuses an unreadable one.
    [TestMethod]
    public async Task StartAsync_HttpsWithAnUnreadableCertificateFile_ThrowsCertificateProblemWithSurlsText()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("https://127.0.0.1:0");
        options.CertificateFile = "surl-api-no-such-cert.pem";

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(SurlExitCode.CertificateProblem, exception.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "(58) Could not load the server certificate: Cannot read the --cert file 'surl-api-no-such-cert.pem'." },
            exception.Lines.ToArray());
        Assert.AreEqual(0, seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task StartAsync_HttpsSelfSigned_StartsAndWritesTheSelfSignedWarning()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("https://127.0.0.1:0");
        options.SelfSigned = true;
        options.Log = new StringWriter();

        await using (await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken))
        {
        }

        Assert.Contains("surl: warning: --self-signed:", options.Log.ToString()!);
    }

    [TestMethod]
    public async Task StartAsync_SftpWithAMissingHostKeyFile_ThrowsSurlsRefusal()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("sftp://127.0.0.1:0");
        options.HostKeyFiles.Add("missing.key");

        var exception = await Assert.ThrowsExactlyAsync<SurlServerStartException>(
            () => SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken));

        Assert.AreEqual(SurlExitCode.CouldNotReadFile, exception.ExitCode);
        Assert.AreEqual(0, seams.ListenerFactory.OpenListenerCount);
    }

    [TestMethod]
    public async Task StartAsync_SftpWithAThrowawayHostKey_StartsAndWritesTheThrowawayHostKeyWarning()
    {
        var seams = new ApiTestSeams();
        var options = ApiTestSeams.Options("sftp://127.0.0.1:0");
        options.ThrowawayHostKey = true;
        options.Log = new StringWriter();

        await using (await SurlServer.StartAsync(options, seams.Host, TestContext.CancellationToken))
        {
        }

        Assert.Contains("surl: warning: --throwaway-hostkey", options.Log.ToString()!);
    }
}
