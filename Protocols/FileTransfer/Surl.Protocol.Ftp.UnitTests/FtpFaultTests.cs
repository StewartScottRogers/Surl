using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Surl.Content;
using Surl.Core;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ftp.FtpTestExchange;
using static Surl.Protocol.Ftp.FtpUploadTests;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4): a command a matching <see cref="FaultKind.Error"/>
/// rule names is answered with the rule's reply in place of running, after the login check and
/// before the content store, opening no data connection, and publishes one
/// <see cref="FaultAppliedEvent"/>. The recorded cases (Fixtures/README.md, "Chosen-error faults
/// (BL-794)") are what pinned upstream curl 8.21.0 sent and how it exited when its server answered so.
/// </summary>
[TestClass]
public sealed class FtpFaultTests
{
    private const string FaultReply = "451 Requested action aborted: fault injected";
    private const string PassiveReply = "229 Entering Extended Passive Mode (|||50100|)\r\n";

    private static readonly IPEndPoint PassiveEndPoint = new(IPAddress.Loopback, 50100);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Replay_StorUnderAnErrorRuleOnUp_Answers451OpensNoDataConnectionAndLeavesTheStore()
    {
        var fileSystem = FileSystemWithUp();
        var events = new RecordingSurlEventPublisher();
        var dataConnections = new InMemoryDataConnections()
            .ScriptPassiveListener(new IPEndPoint(IPAddress.Loopback, AnnouncedPassivePort("fault-stor")), new InMemoryConnection(Ascii("uploaded body\n")));
        var control = new InMemoryConnection([RecordedFixture.ReadRequestBytes("fault-stor")]);

        await Server(contentStore: Store(fileSystem)).ServeAsync(
            control, FaultedContext(Plan(Rule(command: "STOR", path: "/up")), events, dataConnections));

        Assert.AreEqual(RecordedFixture.ReadServerReplies("fault-stor"), Text(control.WrittenBytes));
        Assert.IsEmpty(dataConnections.PassiveListeners.Single().AcceptTimeouts);
        Assert.IsEmpty(UpEntries(fileSystem));
        AssertOneFaultApplied(events, "STOR /up/a.txt");
        Assert.AreEqual("25", ExitCode("fault-stor"));
    }

    [TestMethod]
    public async Task Replay_RetrUnderAnErrorRule_Answers451AndSendsNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var dataConnection = new InMemoryConnection([]);
        var dataConnections = new InMemoryDataConnections()
            .ScriptPassiveListener(new IPEndPoint(IPAddress.Loopback, AnnouncedPassivePort("fault-retr")), dataConnection);
        var control = new InMemoryConnection([RecordedFixture.ReadRequestBytes("fault-retr")]);

        await Server().ServeAsync(control, FaultedContext(Plan(Rule(command: "RETR", path: "/a.txt")), events, dataConnections));

        Assert.AreEqual(RecordedFixture.ReadServerReplies("fault-retr"), Text(control.WrittenBytes));
        Assert.IsEmpty(dataConnections.PassiveListeners.Single().AcceptTimeouts);
        Assert.IsEmpty(dataConnection.WrittenBytes);
        AssertOneFaultApplied(events, "RETR /a.txt");
        Assert.AreEqual("19", ExitCode("fault-retr"));
    }

    [TestMethod]
    public async Task Stor_OutsideTheRulesPath_IsServed()
    {
        var fileSystem = FileSystemWithUp();
        var events = new RecordingSurlEventPublisher();

        var written = await ServeLoggedInWithFaultsAsync(
            "EPSV\r\nSTOR /b.txt\r\n", Plan(Rule(command: "STOR", path: "/up")), events, Store(fileSystem), Passive(new InMemoryConnection(Ascii("uploaded body\n"))));

        Assert.AreEqual(PassiveReply + "150 Opening data connection for /b.txt\r\n226 Transfer complete\r\n", written);
        Assert.AreEqual("uploaded body\n", ReadFile(fileSystem, "b.txt"));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task Retr_OutsideTheRulesPath_IsServed()
    {
        var events = new RecordingSurlEventPublisher();
        var dataConnection = new InMemoryConnection([]);

        var written = await ServeLoggedInWithFaultsAsync(
            "EPSV\r\nRETR /a.txt\r\n", Plan(Rule(command: "RETR", path: "/dir")), events, StandardContentStore(), Passive(dataConnection));

        Assert.AreEqual(PassiveReply + "150 Opening data connection for /a.txt (12 bytes)\r\n226 Transfer complete\r\n", written);
        Assert.AreEqual(FileText, Text(dataConnection.WrittenBytes));
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    [DataRow("alice", true)]
    [DataRow("bob", false)]
    public async Task Retr_RuleScopedToAlice_FiresOnlyForAlice(string userName, bool isAnsweredByTheFault)
    {
        var events = new RecordingSurlEventPublisher();
        var connection = new InMemoryConnection(Ascii($"USER {userName}\r\nPASS secret\r\nEPSV\r\nRETR a.txt\r\n"));
        var plan = Plan(new FaultRule("x", FaultKind.Error) { Scheme = "ftp", Account = "alice", Command = "RETR", Reply = FaultReply });

        await Server(new UnitTestRecordingAuthenticationPolicy(PasswordLoginVerdict.Accepted)).ServeAsync(
            connection, FaultedContext(plan, events, Passive(new InMemoryConnection([]))));

        var written = Text(connection.WrittenBytes)[(Greeting + AnonymousLoginReplies).Length..];
        Assert.AreEqual(isAnsweredByTheFault ? 1 : 0, events.Events.Count);
        Assert.AreEqual(isAnsweredByTheFault, written.Contains(FaultReply + "\r\n", StringComparison.Ordinal), written);
    }

    [TestMethod]
    public async Task Retr_RuleScopedToAnAccount_NeverFiresAfterAnUncheckedAnonymousLogin()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("x", FaultKind.Error) { Account = "anonymous", Reply = FaultReply });

        await ServeLoggedInWithFaultsAsync("RETR a.txt\r\n", plan, events, StandardContentStore());

        Assert.IsNull(plan.Points.Last().Account);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task Command_AskedAbout_CarriesTheExchangeListenUrlSchemeAndCommand()
    {
        var plan = new ScriptedFaultPlan();

        await ServeLoggedInWithFaultsAsync("NOOP\r\n", plan, new RecordingSurlEventPublisher(), StandardContentStore());

        var point = plan.Points.Single(asked => asked.Command == "NOOP");
        Assert.AreEqual(new FaultPoint(FaultStage.Command)
        {
            ExchangeId = 1,
            ListenUrl = "ftp://127.0.0.1:2121/",
            Scheme = "ftp",
            Command = "NOOP",
        }, point);
    }

    [TestMethod]
    [DataRow("CWD dir/sub", "/dir/sub", DisplayName = "a relative path is resolved against the current directory")]
    [DataRow("LIST -la dir", "/dir", DisplayName = "LIST's ls options are not part of the path")]
    [DataRow("NLST", "/", DisplayName = "a listing without a path is the current directory")]
    [DataRow("RETR ../..", null, DisplayName = "a path above / has none")]
    [DataRow("PWD", null, DisplayName = "a command naming no path has none")]
    public async Task Command_AskedAbout_CarriesThePathItNames(string commandLine, string? expectedPath)
    {
        var plan = new ScriptedFaultPlan();

        await ServeLoggedInWithFaultsAsync(commandLine + "\r\n", plan, new RecordingSurlEventPublisher(), StandardContentStore());

        Assert.AreEqual(expectedPath, plan.Points.Last().Path);
    }

    [TestMethod]
    public async Task Command_NotImplementedOrNeedingALoginFirst_IsNeverAskedAbout()
    {
        var plan = new ScriptedFaultPlan();

        await ServeWithFaultsAsync("RETR a.txt\r\nXYZZY\r\n", plan, new RecordingSurlEventPublisher(), StandardContentStore());

        Assert.IsEmpty(plan.Points);
    }

    [TestMethod]
    public async Task Command_MatchedByAnErrorRuleWithoutAReply_RunsAsUsual()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("x", FaultKind.Error) { Command = "PWD" });

        var written = await ServeLoggedInWithFaultsAsync("PWD\r\n", plan, events, StandardContentStore());

        Assert.AreEqual("257 \"/\" is the current directory\r\n", written);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    public async Task Retr_UnderADelayRule_IsAnsweredOnlyAfterTheClockAdvances()
    {
        var clock = new ManualTimeProvider();
        var idleClock = new RecordingIdleClock();
        var events = new RecordingSurlEventPublisher();
        var dataConnection = new InMemoryConnection([]);
        var plan = new ScriptedFaultPlan(new FaultRule("d1", FaultKind.Delay) { Stage = FaultStage.Command, Command = "RETR", DelayMilliseconds = 500 });
        var control = new InMemoryConnection(Ascii(AnonymousLogin + "EPSV\r\nRETR /a.txt\r\n"));
        var context = Context(clock, TestContext.CancellationToken, dataConnections: Passive(dataConnection)) with
        {
            Faults = plan,
            Events = events,
            IdleClock = idleClock,
        };

        var serving = Server().ServeAsync(control, context);
        await idleClock.FirstPause;
        var writtenWhileHeld = Text(control.WrittenBytes);
        var pausedWhileHeld = idleClock.IsPaused;
        clock.Advance(TimeSpan.FromMilliseconds(500));
        await serving;

        Assert.DoesNotContain("150 ", writtenWhileHeld);
        Assert.IsTrue(pausedWhileHeld);
        Assert.IsFalse(idleClock.IsPaused);
        Assert.AreEqual(FileText, Text(dataConnection.WrittenBytes));
        Assert.AreEqual(new FaultAppliedEvent("d1", FaultKind.Delay, FaultStage.Command, 1, "RETR /a.txt"), events.Events.Single());
    }

    [TestMethod]
    [DataRow("RETR /a.txt", "/a.txt", DisplayName = "RETR")]
    [DataRow("STOR /b.txt", "/b.txt", DisplayName = "STOR")]
    public async Task Command_UnderADropRule_WritesNothingMoreAndEndsTheSession(string command, string path)
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("x1", FaultKind.Drop) { Stage = FaultStage.Command, Command = command[..4] });
        var control = new InMemoryConnection(Ascii(AnonymousLogin + command + "\r\nNOOP\r\n"));

        await Server(contentStore: StandardContentStore()).ServeAsync(control, FaultedContext(plan, events, Passive(new InMemoryConnection([]))));

        Assert.AreEqual(Greeting + AnonymousLoginReplies, Text(control.WrittenBytes));
        Assert.IsTrue(control.Aborted);
        Assert.AreEqual(new FaultAppliedEvent("x1", FaultKind.Drop, FaultStage.Command, 1, command[..4] + " " + path), events.Events.Single());
    }

    [TestMethod]
    public async Task Command_AnsweredByAFault_IsNotedInTheExchangeLog()
    {
        var log = new RecordingExchangeLog();
        var plan = new ScriptedFaultPlan(new FaultRule("f7", FaultKind.Error) { Command = "PWD", Reply = "550 Not now" });

        var written = await ServeLoggedInWithFaultsAsync("PWD\r\nNOOP\r\n", plan, new RecordingSurlEventPublisher(), StandardContentStore(), log: log);

        Assert.AreEqual("550 Not now\r\n200 NOOP ok\r\n", written);
        CollectionAssert.Contains(log.Notes.ToList(), "PWD answered by fault rule f7: 550 Not now");
    }

    private static FaultPlan Plan(FaultRule rule)
    {
        var plan = new FaultPlan();
        plan.Add(rule);
        return plan;
    }

    private static FaultRule Rule(string command, string path) =>
        new("x", FaultKind.Error) { Scheme = "ftp", Command = command, Path = path, Reply = FaultReply };

    private static void AssertOneFaultApplied(RecordingSurlEventPublisher events, string target) =>
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, target), events.Events.Single());

    private ExchangeContext FaultedContext(IFaultPlan plan, ISurlEventPublisher events, IDataConnectionOpener? dataConnections = null, IExchangeLog? log = null) =>
        Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log, dataConnections: dataConnections) with { Faults = plan, Events = events };

    private async Task<string> ServeWithFaultsAsync(
        string request, IFaultPlan plan, ISurlEventPublisher events, ContentStore contentStore, IDataConnectionOpener? dataConnections = null, IExchangeLog? log = null)
    {
        var connection = new InMemoryConnection(Ascii(request));
        await Server(contentStore: contentStore).ServeAsync(connection, FaultedContext(plan, events, dataConnections, log));

        return Text(connection.WrittenBytes);
    }

    private async Task<string> ServeLoggedInWithFaultsAsync(
        string commands, IFaultPlan plan, ISurlEventPublisher events, ContentStore contentStore, IDataConnectionOpener? dataConnections = null, IExchangeLog? log = null)
    {
        var written = await ServeWithFaultsAsync(AnonymousLogin + commands, plan, events, contentStore, dataConnections, log);
        Assert.StartsWith(Greeting + AnonymousLoginReplies, written);

        return written[(Greeting + AnonymousLoginReplies).Length..];
    }

    private static InMemoryContentFileSystem FileSystemWithUp()
    {
        var fileSystem = StandardFileSystem();
        fileSystem.CreateDirectory(Path.Join(InMemoryContentFileSystem.RootPath, "up"));
        return fileSystem;
    }

    private static string[] UpEntries(InMemoryContentFileSystem fileSystem) =>
        [.. fileSystem.EnumerateDirectoryEntryNames(Path.Join(InMemoryContentFileSystem.RootPath, "up"))];

    private static InMemoryDataConnections Passive(IConnection dataConnection) =>
        new InMemoryDataConnections().ScriptPassiveListener(PassiveEndPoint, dataConnection);

    private static string ExitCode(string caseName) => Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "exitcode.txt"));

    private static int AnnouncedPassivePort(string caseName)
    {
        var transcript = Encoding.ASCII.GetString(RecordedFixture.ReadBytes(caseName, "transcript.txt"));
        var extended = Regex.Match(transcript, @"^< 229 .*\(\|\|\|(\d+)\|\)", RegexOptions.Multiline);
        return int.Parse(extended.Groups[1].Value, CultureInfo.InvariantCulture);
    }
}
