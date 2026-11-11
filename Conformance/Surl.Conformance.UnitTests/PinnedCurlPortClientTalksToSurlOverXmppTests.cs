using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port client's <c>xmpp</c> extension connects to a live <c>surl xmpp://</c> (STARTTLS) and
/// <c>xmpps://</c> (ADR-0083 decisions 1 to 3, BL-541). Every assertion is a rule of RFC 6120 or XEP-0030 as
/// ADR-0114 decides it, listed in <c>Documentation/Conformance/Extended/Xmpp.md</c>; what the client prints is
/// never an expected value beyond the answer the server was required to send. Inconclusive when no pinned
/// client is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class PinnedCurlPortClientTalksToSurlOverXmppTests
{
    private const int LoginDeniedExitCode = 67;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Xmpp:RFC6120-5.4.2.3-1, Xmpp:RFC7677-3-1, Xmpp:RFC6120-7.6.1-1: STARTTLS, SCRAM-SHA-256 and a bind let the client send a message")]
    [DataRow("xmpp")]
    [DataRow("xmpps")]
    public async Task SendMessage_AfterScramLogin_Succeeds(string scheme)
    {
        await using var surl = await StartAsync(scheme);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-k", "-u", "alice:secret", "-d", "hi", Url(surl, scheme));

        Assert.AreEqual(0, result.ExitCode, Diagnostics(result, surl));
    }

    [TestMethod]
    [Description("Xmpp:XEP0030-3.1-2: a disco#info query to the client's own bare JID is answered with identity account/registered")]
    [DataRow("xmpp")]
    [DataRow("xmpps")]
    public async Task DiscoInfo_ToTheOwnBareJid_PrintsTheAccountIdentity(string scheme)
    {
        await using var surl = await StartAsync(scheme);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-k", "-u", "alice:secret", Url(surl, scheme));

        Assert.AreEqual(0, result.ExitCode, Diagnostics(result, surl));
        var text = Encoding.UTF8.GetString(result.StandardOutput.ToArray());
        StringAssert.Contains(text, "http://jabber.org/protocol/disco#info", Diagnostics(result, surl));
        StringAssert.Contains(text, "registered", Diagnostics(result, surl));
    }

    [TestMethod]
    [Description("Xmpp:RFC6120-6.5-1: a wrong password is not-authorized and the client exits with its login-denied code")]
    [DataRow("xmpp")]
    [DataRow("xmpps")]
    public async Task Login_WithAWrongPassword_IsRefused(string scheme)
    {
        await using var surl = await StartAsync(scheme);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-k", "-u", "alice:wrong", "-d", "hi", Url(surl, scheme));

        Assert.AreEqual(LoginDeniedExitCode, result.ExitCode, Diagnostics(result, surl));
        StringAssert.Contains(result.StandardError, "not-authorized", Diagnostics(result, surl));
        Assert.IsEmpty(result.StandardOutput.ToArray(), Diagnostics(result, surl));
    }

    private Task<SurlOnLoopback> StartAsync(string scheme) =>
        SurlOnLoopback.StartAsync(scheme, new Dictionary<string, byte[]>(), [], ["--user", "alice:secret", "--self-signed"], TestContext.CancellationToken);

    private static string Url(SurlOnLoopback surl, string scheme) =>
        $"{scheme}://127.0.0.1:{surl.BaseUrl.Port}/alice@127.0.0.1";

    private static string Diagnostics(CurlPortClientRunResult result, SurlOnLoopback surl) =>
        $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
}
