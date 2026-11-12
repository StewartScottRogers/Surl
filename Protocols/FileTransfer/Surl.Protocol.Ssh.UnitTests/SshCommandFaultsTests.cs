using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SftpTestPackets;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4) in the SFTP subsystem and SCP: a rule for an SFTP
/// packet name and path is answered with <c>SSH_FXP_STATUS</c> carrying its status and message, an
/// SCP command under a rule with byte 1 and the rule's message line, each before the content store
/// is asked and each publishing one <see cref="FaultAppliedEvent"/>. Packets are built by hand from
/// draft-ietf-secsh-filexfer-02, as <see cref="SftpSessionTests"/>' are.
/// </summary>
[TestClass]
public sealed class SshCommandFaultsTests
{
    private const uint WriteCreateTruncate = 0x1A;

    private static readonly ContentExposureOptions Uploads = new() { AllowUploads = true };

    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SftpOpen_UnderARuleForItsPath_IsTheRulesStatusAndTheStoreIsUnchanged_WhileAnotherPathOpens()
    {
        var (store, fileSystem) = StoreWithUpDirectory();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "sftp", Path = "/up/a", Reply = "3 Quota says no" });

        var run = await RunSftpAsync(store, ContentCaller.Anonymous("sftp"), plan, Open(1, "/up/a", WriteCreateTruncate), Open(2, "/b", WriteCreateTruncate));

        CollectionAssert.AreEqual(Status(1, 3, "Quota says no"), run.Replies[0]);
        CollectionAssert.AreEqual(HandleReply(2, 0), run.Replies[1]);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "up", "a")));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/up/a"), run.Events.Single());
        CollectionAssert.Contains(run.Notes, "SFTP OPEN /up/a -> PERMISSION_DENIED: fault rule f1");
        var point = plan.Points[0];
        Assert.AreEqual(new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "sftp://127.0.0.1:47301/", Scheme = "sftp", Path = "/up/a", Command = "OPEN" }, point);
    }

    [TestMethod]
    [DataRow("alice", true)]
    [DataRow("bob", false)]
    public async Task SftpRule_ScopedToAlice_FiresOnlyForAlice(string account, bool fires)
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "sftp", Account = "alice", Reply = "4 Not today" });

        var run = await RunSftpAsync(Store(new(), clock), ContentCaller.ForAccount(account, "sftp"), plan, Open(1, "/a.txt"));

        CollectionAssert.AreEqual(fires ? Status(1, 4, "Not today") : HandleReply(1, 0), run.Replies.Single());
        Assert.HasCount(fires ? 1 : 0, run.Events);
    }

    [TestMethod]
    public async Task SftpRead_UnderARuleForTheHandlesPath_IsTheRulesStatus()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "READ", Path = "/dir/b.txt", Reply = "7 Lost" });

        var run = await RunSftpAsync(Store(new(), clock), ContentCaller.Anonymous("sftp"), plan, Open(1, "/dir/b.txt"), Read(2, 0, 0, 10));

        CollectionAssert.AreEqual(Status(2, 7, "Lost"), run.Replies[1]);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/dir/b.txt"), run.Events.Single());
    }

    [TestMethod]
    [DataRow(5, DisplayName = "READ on a handle never issued")]
    [DataRow(17, DisplayName = "STAT whose path runs past the packet")]
    [DataRow(200, DisplayName = "EXTENDED, which names no path")]
    public async Task SftpRequest_NamingNoPath_IsMatchedByItsCommandAlone(int type)
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Path = null, Reply = "2 Gone" });
        var packet = type == 5 ? Read(3, 9, 0, 1) : Request((byte)type, 3, UInt32(0x7FFFFFFF));

        var run = await RunSftpAsync(Store(new(), clock), ContentCaller.Anonymous("sftp"), plan, packet);

        CollectionAssert.AreEqual(Status(3, 2, "Gone"), run.Replies.Single());
        Assert.IsNull(plan.Points.Single().Path);
        Assert.AreEqual(SftpPacketType.NameOf((byte)type), ((FaultAppliedEvent)run.Events.Single()).Target);
    }

    [TestMethod]
    public async Task SftpStat_PathNotUtf8_IsMatchedWithNoPath()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "STAT", Reply = "2 Gone" });

        var run = await RunSftpAsync(Store(new(), clock), ContentCaller.Anonymous("sftp"), plan, Request(17, 4, Bytes([0x2F, 0xFF])));

        CollectionAssert.AreEqual(Status(4, 2, "Gone"), run.Replies.Single());
        Assert.IsNull(plan.Points.Single().Path);
    }

    [TestMethod]
    public async Task SftpOpenDirectory_WithATrailingSlash_IsMatchedWithoutIt()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Path = "/dir", Reply = "3 No" });

        var run = await RunSftpAsync(Store(new() { ListDirectories = true }, clock), ContentCaller.Anonymous("sftp"), plan, Request(11, 5, Text("/dir/")));

        CollectionAssert.AreEqual(Status(5, 3, "No"), run.Replies.Single());
    }

    [TestMethod]
    [DataRow(FaultKind.RefuseLogin)]
    public async Task SftpRule_OfAnotherKind_IsNotAppliedAtACommand(FaultKind kind)
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", kind) { Reply = "3 No" });

        var run = await RunSftpAsync(Store(new(), clock), ContentCaller.Anonymous("sftp"), plan, Open(1, "/a.txt"));

        CollectionAssert.AreEqual(HandleReply(1, 0), run.Replies.Single());
        Assert.IsEmpty(run.Events);
        CollectionAssert.Contains(run.Notes, $"Fault rule f1 not applied to sftp OPEN: a {kind} rule is not applied at a command");
    }

    [TestMethod]
    public async Task ScpUpload_UnderARule_IsByteOneAndTheRulesLineAndTheStoreIsUnchanged()
    {
        var (store, fileSystem) = StoreWithUpDirectory();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "scp", Path = "/up/a", Reply = "disk on fire" });

        var run = await RunScpAsync(store, plan, new SshScpCommand(false, false, false, "/up/a"), Ascii("C0644 5 a\n"), Ascii("hello"));

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("\u0001disk on fire\n", run.Text);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "up", "a")));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/up/a"), run.Events.Single());
        CollectionAssert.AreEqual(new[] { "SCP upload /up/a refused by fault rule f1: disk on fire" }, run.Notes);
        Assert.AreEqual(new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "sftp://127.0.0.1:47301/", Scheme = "scp", Path = "/up/a", Command = "scp" }, plan.Points.Single());
    }

    [TestMethod]
    public async Task ScpUpload_UnderNoMatchingRule_IsAcknowledgedAsUsual()
    {
        var (store, _) = StoreWithUpDirectory();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "scp", Path = "/other", Reply = "no" });

        var run = await RunScpAsync(store, plan, new SshScpCommand(false, false, false, "/up/a"), Ascii("C0644 5 a\n"), Ascii("hello"));

        Assert.AreEqual(0u, run.Exit);
        Assert.AreEqual("\0\0\0", run.Text);
        Assert.IsEmpty(run.Events);
    }

    [TestMethod]
    public async Task ScpDownload_UnderARule_IsByteOneAndTheRulesLineAfterTheStart()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "scp", Path = "/a.txt", Reply = "not today" });

        var run = await RunScpAsync(Store(new(), clock), plan, new SshScpCommand(true, false, false, "/a.txt"), [0]);

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual("\u0001not today\n", run.Text);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/a.txt"), run.Events.Single());
    }

    [TestMethod]
    [DataRow("two\nlines")]
    [DataRow("")]
    [DataRow(null)]
    public async Task ScpDownload_UnderARuleWhoseReplyIsNotOneLine_IsSentAsUsualAndNoted(string? reply)
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = reply });

        var run = await RunScpAsync(Store(new(), clock), plan, new SshScpCommand(true, false, false, "/a.txt"), [0], [0], [0]);

        Assert.AreEqual(0u, run.Exit);
        Assert.IsEmpty(run.Events);
        CollectionAssert.Contains(run.Notes, "Fault rule f1 not applied to scp scp: its reply does not fit the scp error shape");
    }

    [TestMethod]
    public async Task SftpOpen_UnderARuleWhoseReplyDoesNotFit_IsAnsweredAsUsualAndNoted()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = "9 Out of range" });

        var run = await RunSftpAsync(Store(new(), clock), ContentCaller.Anonymous("sftp"), plan, Open(1, "/a.txt"));

        CollectionAssert.AreEqual(HandleReply(1, 0), run.Replies.Single());
        Assert.IsEmpty(run.Events);
        CollectionAssert.Contains(run.Notes, "Fault rule f1 not applied to sftp OPEN: its reply does not fit the sftp error shape");
    }

    [TestMethod]
    public async Task ScpDownload_PathHoldingANul_IsMatchedWithNoPath()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "scp", Reply = "no" });

        var run = await RunScpAsync(Store(new(), clock), plan, new SshScpCommand(true, false, false, "/a\0b"), [0]);

        Assert.AreEqual("\u0001no\n", run.Text);
        Assert.AreEqual("scp", ((FaultAppliedEvent)run.Events.Single()).Target);
    }

    [TestMethod]
    [DataRow("2 Gone", true, 2u, "Gone")]
    [DataRow("8 Not here", true, 8u, "Not here")]
    [DataRow("9 Out of range", false, 4u, "")]
    [DataRow("1 Too low", false, 4u, "")]
    [DataRow("word only", false, 4u, "")]
    [DataRow(" 3 leading space", false, 4u, "")]
    [DataRow("3 ", false, 4u, "")]
    [DataRow("3", false, 4u, "")]
    [DataRow(null, false, 4u, "")]
    public void TryReadSftpStatus_ReadsACodeTwoToEightAndText(string? reply, bool fits, uint code, string message)
    {
        var read = SshCommandFaults.TryReadSftpStatus(reply, out var readCode, out var readMessage);

        Assert.AreEqual((fits, (SftpStatusCode)code, message), (read, readCode, readMessage));
    }

    // ADR-0141: a command-stage delay holds OPEN's answer until the clock advances, idle clock paused.
    [TestMethod]
    public async Task SftpOpen_UnderADelayRule_IsHeldUntilTheClockAdvances500Ms_WithTheIdleClockPaused()
    {
        var delay = new FaultRule("f1", FaultKind.Delay) { Stage = FaultStage.Command, Command = "OPEN", DelayMilliseconds = 500 };
        var idleClock = new RecordingIdleClock();
        var events = new RecordingSurlEventPublisher();
        var channel = new SftpTestChannel(Init3, Open(1, "/a.txt"));
        var context = FaultedContext(new ScriptedFaultPlan(delay), new RecordingExchangeLog(), events) with { IdleClock = idleClock };
        var serving = new SftpSession(Store(new(), clock), ContentCaller.Anonymous("sftp"), context).RunAsync(channel, TestContext.CancellationToken);

        await idleClock.FirstPause.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        var packetsAtTheHold = channel.WrittenPackets().Count;
        clock.Advance(TimeSpan.FromMilliseconds(499));
        var packetsAfter499Ms = channel.WrittenPackets().Count;
        var pausedAfter499Ms = idleClock.IsPaused;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var exit = await serving.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Assert.AreEqual(0u, exit);
        Assert.AreEqual(1, packetsAtTheHold);
        Assert.AreEqual(1, packetsAfter499Ms);
        Assert.IsTrue(pausedAfter499Ms);
        Assert.IsFalse(idleClock.IsPaused);
        CollectionAssert.AreEqual(HandleReply(1, 0), channel.WrittenPackets()[1]);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Delay, FaultStage.Command, 1, "/a.txt"), events.Events.Single());
    }

    // ADR-0141: a command-stage drop aborts the connection in place of OPEN's answer and ends the session.
    [TestMethod]
    public async Task SftpOpen_UnderADropRule_AbortsTheConnectionWithNoAnswerAndEndsTheSession()
    {
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Command = "OPEN" };
        var log = new RecordingExchangeLog();
        var events = new RecordingSurlEventPublisher();
        var channel = new SftpTestChannel(Init3, Open(1, "/a.txt"), Open(2, "/b.txt"));

        var exit = await new SftpSession(Store(new(), clock), ContentCaller.Anonymous("sftp"), FaultedContext(new ScriptedFaultPlan(drop), log, events))
            .RunAsync(channel, TestContext.CancellationToken);

        Assert.AreEqual(1u, exit);
        Assert.IsTrue(channel.Connection.Aborted);
        Assert.HasCount(1, channel.WrittenPackets());
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "/a.txt"), events.Events.Single());
        CollectionAssert.Contains(log.Notes.ToList(), "SFTP session ended: dropped by fault rule f1");
    }

    [TestMethod]
    public async Task ScpUpload_UnderADropRule_AbortsTheConnectionAndWritesNothing()
    {
        var (store, fileSystem) = StoreWithUpDirectory();
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Command = "scp" };

        var run = await RunScpAsync(store, new ScriptedFaultPlan(drop), new SshScpCommand(false, false, false, "/up/a"), Ascii("C0644 5 a\n"), Ascii("hello"));

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual(string.Empty, run.Text);
        Assert.IsTrue(run.Aborted);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(InMemoryContentFileSystem.RootPath, "up", "a")));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "/up/a"), run.Events.Single());
    }

    [TestMethod]
    public async Task ScpDownload_UnderADropRule_AbortsTheConnectionAfterTheStartAcknowledgement()
    {
        var drop = new FaultRule("f1", FaultKind.Drop) { Stage = FaultStage.Command, Command = "scp" };

        var run = await RunScpAsync(Store(new(), clock), new ScriptedFaultPlan(drop), new SshScpCommand(true, false, false, "/a.txt"), [0]);

        Assert.AreEqual(1u, run.Exit);
        Assert.AreEqual(string.Empty, run.Text);
        Assert.IsTrue(run.Aborted);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Drop, FaultStage.Command, 1, "/a.txt"), run.Events.Single());
    }

    private (ContentStore Store, InMemoryContentFileSystem FileSystem) StoreWithUpDirectory()
    {
        InMemoryContentFileSystem? captured = null;
        var store = Store(Uploads, clock, fileSystem =>
        {
            fileSystem.CreateDirectory(Path.Join(InMemoryContentFileSystem.RootPath, "up"));
            captured = fileSystem;
        });

        return (store, captured!);
    }

    private ExchangeContext FaultedContext(IFaultPlan plan, RecordingExchangeLog log, RecordingSurlEventPublisher events) =>
        Context(clock, TestContext.CancellationToken, null, log) with { Faults = plan, Events = events };

    private async Task<SftpRun> RunSftpAsync(ContentStore store, ContentCaller caller, IFaultPlan plan, params byte[][] requests)
    {
        var log = new RecordingExchangeLog();
        var events = new RecordingSurlEventPublisher();
        var channel = new SftpTestChannel([Init3, .. requests]);
        var exit = await new SftpSession(store, caller, FaultedContext(plan, log, events)).RunAsync(channel, TestContext.CancellationToken);
        var replies = channel.WrittenPackets();

        Assert.AreEqual(0u, exit);
        CollectionAssert.AreEqual(Version3, replies[0]);

        return new SftpRun(replies[1..], [.. log.Notes], [.. events.Events]);
    }

    private async Task<ScpRun> RunScpAsync(ContentStore store, IFaultPlan plan, SshScpCommand command, params byte[][] inbound)
    {
        var log = new RecordingExchangeLog();
        var events = new RecordingSurlEventPublisher();
        var context = FaultedContext(plan, log, events);
        var caller = ContentCaller.Anonymous("scp");
        ISshChannelHandler handler = command.IsSource
            ? new ScpDownloadHandler(store, caller, command, context)
            : new ScpUploadHandler(store, caller, command, context);
        var channel = new SftpTestChannel(inbound);

        var exit = await handler.RunAsync(channel, TestContext.CancellationToken);

        return new ScpRun(exit, Encoding.UTF8.GetString([.. channel.Written]), [.. log.Notes], [.. events.Events], channel.Connection.Aborted);
    }

    private sealed record SftpRun(List<byte[]> Replies, string[] Notes, SurlEvent[] Events);

    private sealed record ScpRun(uint Exit, string Text, string[] Notes, SurlEvent[] Events, bool Aborted);
}
