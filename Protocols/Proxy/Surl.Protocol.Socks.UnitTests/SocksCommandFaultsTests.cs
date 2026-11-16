using System.Globalization;
using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Socks.SocksTestKit;

namespace Surl.Protocol.Socks;

/// <summary>
/// The chosen-error, drop and delay faults (ADR-0124 decisions 4 and 9) at a SOCKS <c>CONNECT</c>:
/// a rule for the listener's scheme or the target is answered as the failed <c>CONNECT</c>'s reply -
/// SOCKS4 <c>00 5B</c> to <c>00 5D</c>, SOCKS5 <c>05 01</c> to <c>05 08</c>, whose exit code pinned
/// upstream curl 8.21.0 reported as 97 for each - before any origin is tried, publishing one
/// <see cref="FaultAppliedEvent"/>.
/// </summary>
[TestClass]
public sealed class SocksCommandFaultsTests
{
    private static readonly byte[] Loopback = [127, 0, 0, 1];

    private static readonly byte[] Socks5Greeting = [0x05, 0x01, 0x00];

    [TestMethod]
    [DataRow("91", 0x5B)]
    [DataRow("92", 0x5C)]
    [DataRow("93", 0x5D)]
    public async Task Socks4Connect_UnderAnErrorRule_IsTheChosenReplyAndNoOriginIsTried(string reply, int wire)
    {
        var run = await RunAsync("socks4", Socks4Connect(), null, new FaultRule("f1", FaultKind.Error) { Scheme = "socks4", Reply = reply });

        CollectionAssert.AreEqual(new byte[] { 0x00, (byte)wire, 0, 0, 0, 0, 0, 0 }, run.Client.WrittenBytes);
        Assert.IsTrue(run.Client.WritesCompleted);
        Assert.IsEmpty(run.Origins.Requests);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "127.0.0.1:8080"), run.Events.Events.Single());
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "socks4://127.0.0.1:1080/", Scheme = "socks4", Path = "127.0.0.1:8080", Command = "CONNECT" },
            run.Plan.Points.Single());
        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"CONNECT 127.0.0.1:8080 answered {wire:X2} by fault rule f1."), run.Log.Notes);
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("5")]
    [DataRow("8")]
    public async Task Socks5Connect_UnderAnErrorRule_IsTheChosenReplyAndNoOriginIsTried(string reply)
    {
        var run = await RunAsync("socks5", Socks5Connect(), null, new FaultRule("f1", FaultKind.Error) { Command = "CONNECT", Reply = reply });

        byte[] expected = [0x05, 0x00, .. Socks5Failure(byte.Parse(reply, CultureInfo.InvariantCulture))];
        CollectionAssert.AreEqual(expected, run.Client.WrittenBytes);
        Assert.IsEmpty(run.Origins.Requests);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "127.0.0.1:8080"), run.Events.Events.Single());
    }

    [TestMethod]
    public async Task Socks5Connect_ToAnotherTarget_IsServedAndNothingIsPublished()
    {
        var run = await RunAsync("socks5", Socks5Connect(), Origin(), new FaultRule("f1", FaultKind.Error) { Path = "127.0.0.1:9", Reply = "4" });

        byte[] expected = [0x05, 0x00, .. Socks5Succeeded];
        CollectionAssert.AreEqual(expected, run.Client.WrittenBytes);
        Assert.IsEmpty(run.Events.Events);
    }

    [TestMethod]
    [DataRow("socks4")]
    [DataRow("socks5")]
    public async Task Connect_UnderADropRule_AbortsTheConnectionWithoutAReply(string scheme)
    {
        var request = scheme == "socks4" ? Socks4Connect() : Socks5Connect();

        var run = await RunAsync(scheme, request, null, new FaultRule("f1", FaultKind.Drop) { Scheme = scheme });

        Assert.IsTrue(run.Client.Aborted);
        CollectionAssert.AreEqual(scheme == "socks4" ? Array.Empty<byte>() : new byte[] { 0x05, 0x00 }, run.Client.WrittenBytes);
        Assert.IsEmpty(run.Origins.Requests);
        Assert.AreEqual(FaultKind.Drop, ((FaultAppliedEvent)run.Events.Events.Single()).Kind);
    }

    [TestMethod]
    public async Task Connect_UnderADelayRule_IsHeldThenServedAsUsual()
    {
        var run = await RunAsync("socks4", Socks4Connect(), Origin(), new FaultRule("f1", FaultKind.Delay) { DelayMilliseconds = 0 });

        CollectionAssert.AreEqual(Granted, run.Client.WrittenBytes);
        Assert.AreEqual(FaultKind.Delay, ((FaultAppliedEvent)run.Events.Events.Single()).Kind);
    }

    [TestMethod]
    [DataRow("socks4", "90")]
    [DataRow("socks4", "94")]
    [DataRow("socks4", "5")]
    [DataRow("socks5", "0")]
    [DataRow("socks5", "9")]
    [DataRow("socks5", "91")]
    [DataRow("socks5", "five")]
    [DataRow("socks5", null)]
    public async Task Connect_UnderAnErrorRuleWhoseReplyDoesNotFit_IsServedAndTheRuleNotedAsNotApplied(string scheme, string? reply)
    {
        var request = scheme == "socks4" ? Socks4Connect() : Socks5Connect();

        var run = await RunAsync(scheme, request, Origin(), new FaultRule("f1", FaultKind.Error) { Reply = reply });

        Assert.IsEmpty(run.Events.Events);
        Assert.Contains($"Fault rule f1 not applied to CONNECT 127.0.0.1:8080: its reply does not fit the {scheme} error shape", run.Log.Notes);
        Assert.HasCount(1, run.Origins.Requests);
    }

    [TestMethod]
    public async Task Connect_UnderARuleOfAnotherKind_IsServedAndTheRuleNotedAsNotApplied()
    {
        var run = await RunAsync("socks4", Socks4Connect(), Origin(), new FaultRule("f1", FaultKind.RefuseLogin));

        Assert.IsEmpty(run.Events.Events);
        Assert.Contains("Fault rule f1 not applied to CONNECT 127.0.0.1:8080: a RefuseLogin rule is not applied at a command", run.Log.Notes);
        CollectionAssert.AreEqual(Granted, run.Client.WrittenBytes);
    }

    [TestMethod]
    public async Task Connect_WithNoRule_IsServedAsBefore()
    {
        var run = await RunAsync("socks4", Socks4Connect(), Origin());

        CollectionAssert.AreEqual(Granted, run.Client.WrittenBytes);
        Assert.IsEmpty(run.Events.Events);
    }

    private static byte[] Socks5LoginAndConnect(string user, string password) =>
    [
        0x05, 0x01, 0x02, 0x01, (byte)user.Length, .. System.Text.Encoding.ASCII.GetBytes(user), (byte)password.Length, .. System.Text.Encoding.ASCII.GetBytes(password),
        .. Socks5Request(0x01, 0x01, Loopback, 8080),
    ];

    [TestMethod]
    public async Task Socks5Connect_LoggedInAsTheRulesAccount_IsTheChosenReplyAndThePointNamesTheAccount()
    {
        var run = await RunWithAccountsAsync(
            "socks5", Socks5LoginAndConnect("alice", "secret"), null, new ScriptedProxyAccounts(), new FaultRule("f1", FaultKind.Error) { Account = "alice", Reply = "2" });

        byte[] expected = [0x05, 0x02, 0x01, 0x00, .. Socks5Failure(2)];
        CollectionAssert.AreEqual(expected, run.Client.WrittenBytes);
        Assert.AreEqual("alice", run.Plan.Points.Single().Account);
        Assert.HasCount(1, run.Events.Events);
    }

    [TestMethod]
    public async Task Socks5Connect_LoggedInAsAnotherAccount_IsNotHeldByTheRuleAndIsGranted()
    {
        var accounts = new ScriptedProxyAccounts(alice: PasswordLoginVerdict.Accepted, refused: PasswordLoginVerdict.AcceptedUnchecked);

        var run = await RunWithAccountsAsync(
            "socks5", Socks5LoginAndConnect("bob", "pw"), Origin(), accounts, new FaultRule("f1", FaultKind.Error) { Account = "alice", Reply = "2" });

        Assert.IsEmpty(run.Events.Events);
        Assert.AreEqual("bob", run.Plan.Points.Single().Account);
        Assert.HasCount(1, run.Origins.Requests);
    }

    [TestMethod]
    public async Task Connect_OfAnAnonymousSocks5SessionAndOfSocks4_LeavesThePointsAccountNull()
    {
        var anonymous = await RunAsync("socks5", Socks5Connect(), Origin());
        var socks4 = await RunAsync("socks4", Socks4Connect(), Origin());

        Assert.IsNull(anonymous.Plan.Points.Single().Account);
        Assert.IsNull(socks4.Plan.Points.Single().Account);
    }
    private static byte[] Socks4Connect() => Socks4Request(0x01, 8080, Loopback, "");

    private static byte[] Socks5Connect() => [.. Socks5Greeting, .. Socks5Request(0x01, 0x01, Loopback, 8080)];

    private static InMemoryConnection Origin() => new([], localEndPoint: new IPEndPoint(IPAddress.Loopback, 8080));

    private static Task<Run> RunAsync(string scheme, byte[] request, InMemoryConnection? origin, params FaultRule[] rules) =>
        RunWithAccountsAsync(scheme, request, origin, null, rules);

    private static async Task<Run> RunWithAccountsAsync(
        string scheme, byte[] request, InMemoryConnection? origin, IAuthenticationPolicy? accounts, params FaultRule[] rules)
    {
        var client = ConnectionOf(request);
        var origins = new InMemoryOriginConnector();
        if (origin is not null)
        {
            origins.ScriptConnection("127.0.0.1", 8080, origin);
        }

        var log = new RecordingExchangeLog();
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(rules);
        var context = new ExchangeContext(
            1, new ListenUrl(scheme, "127.0.0.1", 1080), new IPEndPoint(IPAddress.Loopback, 1080), new IPEndPoint(IPAddress.Loopback, 50000),
            log, new ManualTimeProvider(Now), CancellationToken.None)
        {
            Faults = plan,
            Events = events,
        };

        await new SocksProtocolServer(origins, accounts).ServeAsync(client, context);

        return new Run(client, origins, log, events, plan);
    }

    private sealed record Run(InMemoryConnection Client, InMemoryOriginConnector Origins, RecordingExchangeLog Log, RecordingSurlEventPublisher Events, ScriptedFaultPlan Plan);
}
