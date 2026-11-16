using Surl.Protocol.Abstractions;

namespace Surl.Api;

// BL-1218: reading back the Redis keyspace (ADR-0164, decision 9).
public sealed partial class SurlServerTests
{
    [TestMethod]
    public async Task Keys_ReadBack_GetsAKeyByNameOrBytesAndListsByGlob_WithoutPublishingAnything()
    {
        var token = TestContext.CancellationToken;
        await using var server = await SurlServer.StartAsync(LookupOptions(RedisUrl), new ApiTestSeams().Host, token);
        await server.Keys.SetAsync("user:1", "alice", TimeSpan.FromHours(1), cancellationToken: token);
        await server.Keys.SetAsync("user:2", "bob", cancellationToken: token);
        await server.Keys.SetAsync("other", "x", cancellationToken: token);
        var before = server.FindEvents(new SurlEventQuery()).Count;

        var one = await server.Keys.GetAsync("user:1", cancellationToken: token);
        var raw = await server.Keys.GetAsync("user:2"u8.ToArray(), 0, token);
        var page = await server.Keys.ListAsync("user:*", cancellationToken: token);
        var next = await server.Keys.ListAsync("*", "other", 1, 0, token);

        Assert.AreEqual("alice", one.Text);
        Assert.AreEqual("user:1", one.Info.Key);
        Assert.AreEqual("string", one.Info.Type);
        Assert.IsNotNull(one.Info.ExpiresAt);
        Assert.AreEqual("bob", raw.Text);
        Assert.IsNull(raw.Info.ExpiresAt);
        CollectionAssert.AreEqual(new[] { "user:1", "user:2" }, page.Keys.Select(key => key.Key).ToArray());
        Assert.IsFalse(page.HasMore);
        Assert.AreEqual("user:1", next.Keys.Single().Key);
        Assert.IsTrue(next.HasMore);
        Assert.AreEqual(before, server.FindEvents(new SurlEventQuery()).Count);
    }

    [TestMethod]
    public async Task Keys_ReadBack_AnotherTypeHasNoText()
    {
        var info = new SurlKeyInfo("k"u8.ToArray(), "list", 3, null);

        Assert.IsNull(new SurlKeyContents(info, null).Text);
        await Task.CompletedTask;
    }

    [TestMethod]
    public async Task Keys_ReadBack_AbsentKeyIsNotFound_AndBadArgumentsAreRefused()
    {
        var token = TestContext.CancellationToken;
        await using var server = await SurlServer.StartAsync(LookupOptions(RedisUrl), new ApiTestSeams().Host, token);

        await Assert.ThrowsExactlyAsync<SurlNotFoundException>(() => server.Keys.GetAsync("nope", cancellationToken: token));
        await Assert.ThrowsExactlyAsync<SurlReadRefusedException>(() => server.Keys.GetAsync("k", 16, token));
        await Assert.ThrowsExactlyAsync<SurlReadRefusedException>(() => server.Keys.ListAsync("*", null, 0, 0, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Keys.GetAsync((string)null!, 0, token));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.Keys.ListAsync(null!, null, 1, 0, token));
    }

    [TestMethod]
    public async Task Keys_ReadBack_AfterStopAsync_ThrowsObjectDisposedException()
    {
        var token = TestContext.CancellationToken;
        var server = await SurlServer.StartAsync(LookupOptions(RedisUrl), new ApiTestSeams().Host, token);
        _ = await server.StopAsync(token);

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Keys.GetAsync("k", 0, token));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => server.Keys.ListAsync("*", null, 1, 0, token));
    }
}
