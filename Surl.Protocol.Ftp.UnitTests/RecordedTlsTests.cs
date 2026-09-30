using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;

namespace Surl.Protocol.Ftp;

/// <summary>
/// Replays each secure FTP session recorded from pinned upstream curl (Fixtures/README.md,
/// "Secure FTP (BL-181)") and asserts surl answers every line with the reply the recorder fed
/// curl, upgrades the control connection exactly where curl's <c>AUTH SSL</c> asked, and runs
/// a TLS handshake on the data connection exactly when the protection level is private
/// (ADR-0052, decision 5).
/// </summary>
/// <remarks>
/// The recorder upgraded the control connection right after its <c>234</c>, so curl sent
/// nothing more until the handshake was done: the replay hands surl the lines up to
/// <c>AUTH SSL</c> in one read and the rest, which curl sent inside TLS, in the next. The
/// policy refuses a login with no TLS session, as surl does without
/// <c>--allow-plaintext-auth</c>, so an accepted <c>PASS</c> proves it arrived after the
/// upgrade.
/// </remarks>
[TestClass]
public sealed class RecordedTlsTests
{
    private const string AuthLine = "AUTH SSL\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("ssl-reqd", true, DisplayName = "--ssl-reqd: AUTH SSL, PBSZ 0, PROT P")]
    [DataRow("ftp-ssl-control", false, DisplayName = "--ftp-ssl-control: AUTH SSL, PBSZ 0, PROT C")]
    [DataRow("ftp-ssl-ccc", true, DisplayName = "--ssl-reqd --ftp-ssl-ccc: CCC refused, the session stays TLS")]
    public async Task Replay_ExplicitTls_UpgradesAfterAuthAndProtectsDataAsProtSays(string caseName, bool protectsData)
    {
        var request = RecordedFixture.ReadRequestBytes(caseName);
        var authEnd = Encoding.ASCII.GetString(request).IndexOf(AuthLine, StringComparison.Ordinal) + AuthLine.Length;
        var control = new InMemoryConnection([request.AsMemory(0, authEnd), request.AsMemory(authEnd)]);

        var (policy, dataConnection) = await ReplayAsync(caseName, control, "ftp");

        Assert.IsTrue(control.UpgradeRequested);
        Assert.AreEqual(protectsData, dataConnection.UpgradeRequested);
        Assert.AreSame(InMemoryConnection.DefaultUpgradeTlsSession, policy.Logins.Single().TlsSession);
    }

    [TestMethod]
    public async Task Replay_ImplicitFtps_NeedsNoUpgradeAndProtectsData()
    {
        var session = InMemoryConnection.DefaultUpgradeTlsSession;
        var control = new InMemoryConnection([RecordedFixture.ReadRequestBytes("ftps")], initialTlsSession: session);

        var (policy, dataConnection) = await ReplayAsync("ftps", control, "ftps");

        Assert.IsFalse(control.UpgradeRequested);
        Assert.IsTrue(dataConnection.UpgradeRequested);
        Assert.AreSame(session, policy.Logins.Single().TlsSession);
    }

    private async Task<(UnitTestRecordingAuthenticationPolicy Policy, InMemoryConnection DataConnection)> ReplayAsync(
        string caseName, InMemoryConnection control, string scheme)
    {
        var policy = new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted) { RefuseWithoutTls = true };
        var dataConnection = new InMemoryConnection([]);
        var dataConnections = new InMemoryDataConnections()
            .ScriptPassiveListener(new IPEndPoint(IPAddress.Loopback, AnnouncedPassivePort(caseName)), dataConnection);
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken, dataConnections: dataConnections, scheme: scheme);

        await Server(policy, isAuthTlsAvailable: true).ServeAsync(control, context);

        Assert.AreEqual(RecordedFixture.ReadServerReplies(caseName), Text(control.WrittenBytes));
        Assert.AreEqual(FileText, Text(dataConnection.WrittenBytes));
        Assert.AreEqual(FileText, Text(RecordedFixture.ReadBytes(caseName, "stdout.bin")));
        Assert.AreEqual("0", Text(RecordedFixture.ReadBytes(caseName, "exitcode.txt")));
        return (policy, dataConnection);
    }

    private static int AnnouncedPassivePort(string caseName)
    {
        var transcript = Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "transcript.txt"));
        var extended = Regex.Match(transcript, @"^< 229 .*\(\|\|\|(\d+)\|\)", RegexOptions.Multiline);
        return int.Parse(extended.Groups[1].Value, CultureInfo.InvariantCulture);
    }
}
