namespace Surl.Conformance;

/// <summary>
/// The pinned upstream curl build logs in to a live, in-process <c>surl</c> over <c>mqtts</c>
/// and <c>mqtt</c> with the user name and password of its <c>CONNECT</c>, against one account
/// read from a <c>--user-file</c>, and is answered as ADR-0032 section 5 says: a matching
/// account over TLS publishes and subscribes; a wrong password (<c>CONNACK</c> 4), no
/// credentials (<c>CONNACK</c> 5) and a password over <c>mqtt://</c> without
/// <c>--allow-plaintext-auth</c> (<c>CONNACK</c> 5) end curl with exit 8, as BL-115 measured.
/// Inconclusive where no pinned build is installed for the platform.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class UpstreamCurlLogsInToSurlOverMqttTests
{
    // CURLE_WEIRD_SERVER_REPLY: measured by BL-115 for CONNACK 4 and 5,
    // "curl: (8) Expected 0000 but got 000n".
    private const int WeirdServerReply = 8;

    private static readonly string Credentials = $"{AccountsFile.User}:{AccountsFile.Password}";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PublishThenSubscribeOverMqtts_Account_SubscriberReceivesTheMessage()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("mqtts", "--self-signed", "--user-file", accounts.Path);

        var publish = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-k", "-u", Credentials, "-d", "x", TopicUrl(surl));
        var subscribe = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-k", "-u", Credentials, TopicUrl(surl));

        Assert.AreEqual(0, publish.ExitCode, publish.StandardError);
        Assert.IsEmpty(publish.StandardOutput);
        Assert.AreEqual(0, subscribe.ExitCode, subscribe.StandardError);
        CollectionAssert.AreEqual("\0\u0001tx"u8.ToArray(), subscribe.StandardOutput);
    }

    [TestMethod]
    public async Task PublishOverMqtts_WrongPassword_ExitsWeirdServerReply()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("mqtts", "--self-signed", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(
            TestContext, "-sS", "-k", "-u", $"{AccountsFile.User}:wrong", "-d", "x", TopicUrl(surl));

        Assert.AreEqual(WeirdServerReply, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Expected 0000 but got 0004");
    }

    [TestMethod]
    public async Task PublishOverMqtts_NoCredentials_ExitsWeirdServerReply()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("mqtts", "--self-signed", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-k", "-d", "x", TopicUrl(surl));

        Assert.AreEqual(WeirdServerReply, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Expected 0000 but got 0005");
    }

    [TestMethod]
    public async Task PublishOverMqtt_Account_ExitsWeirdServerReply()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("mqtt", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-u", Credentials, "-d", "x", TopicUrl(surl));

        Assert.AreEqual(WeirdServerReply, result.ExitCode, result.StandardError);
        StringAssert.Contains(result.StandardError, "Expected 0000 but got 0005");
    }

    [TestMethod]
    public async Task PublishOverMqtt_AllowPlaintextAuth_ExitsZero()
    {
        using var accounts = await AccountsFile.WriteAsync(TestContext.CancellationToken);
        await using var surl = await StartSurlAsync("mqtt", "--allow-plaintext-auth", "--user-file", accounts.Path);

        var result = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-u", Credentials, "-d", "x", TopicUrl(surl));

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        Assert.IsEmpty(result.StandardOutput);
    }

    private static string TopicUrl(SurlOnLoopback surl) =>
        $"{surl.BaseUrl.Scheme}://{surl.BaseUrl.Host}:{surl.BaseUrl.Port}/t";

    private Task<SurlOnLoopback> StartSurlAsync(string scheme, params string[] options) =>
        SurlOnLoopback.StartInMemoryAsync(scheme, options, TestContext.CancellationToken);
}
