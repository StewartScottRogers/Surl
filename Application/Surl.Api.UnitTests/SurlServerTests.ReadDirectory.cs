using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-1219: reading back the LDAP directory (ADR-0164, decision 9).
public sealed partial class SurlServerTests
{
    [TestMethod]
    public async Task Directory_ExportLdifAsync_ReadsBackWhatAddEntriesAsyncAdded_WithoutPublishingAnything()
    {
        var token = TestContext.CancellationToken;
        await using var server = await SurlServer.StartAsync(LookupOptions(LdapUrl), new ApiTestSeams().Host, token);
        await server.Directory.AddEntriesAsync(DirectoryLdif + "userPassword: secret\n", token);
        var before = server.FindEvents(new SurlEventQuery()).Count;

        var ldif = await server.Directory.ExportLdifAsync(token);

        Assert.AreEqual("version: 1\n\n" + DirectoryLdif, ldif);
        Assert.HasCount(before, server.FindEvents(new SurlEventQuery()));
    }

    [TestMethod]
    public async Task Directory_ExportLdifAsync_AServerWithoutAnLdapListener_ReadsAnEmptyDirectory()
    {
        var token = TestContext.CancellationToken;
        await using var server = await SurlServer.StartAsync(LookupOptions(RedisUrl), new ApiTestSeams().Host, token);

        Assert.AreEqual("version: 1\n", await server.Directory.ExportLdifAsync(token));
    }

    [TestMethod]
    public async Task Directory_ExportLdifAsync_ACancelledToken_ThrowsOperationCanceled()
    {
        var token = TestContext.CancellationToken;
        await using var server = await SurlServer.StartAsync(LookupOptions(LdapUrl), new ApiTestSeams().Host, token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => server.Directory.ExportLdifAsync(cancelled.Token));
    }

    [TestMethod]
    public async Task Directory_ExportLdifAsync_AfterStopAsync_ThrowsObjectDisposedException()
    {
        var token = TestContext.CancellationToken;
        var server = await SurlServer.StartAsync(LookupOptions(LdapUrl), new ApiTestSeams().Host, token);
        _ = await server.StopAsync(token);

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Directory.ExportLdifAsync(token));
    }
}
