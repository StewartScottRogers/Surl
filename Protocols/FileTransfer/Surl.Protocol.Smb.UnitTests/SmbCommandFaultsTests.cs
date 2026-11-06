using Surl.Content;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Smb.SmbTestExchange;

namespace Surl.Protocol.Smb;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4) in the SMB server: a rule for a command and path
/// after the session setup is answered with the rule's NTSTATUS, <c>SMB_FLAGS2_NT_STATUS</c> set,
/// before the content store is asked, publishing one <see cref="FaultAppliedEvent"/>. Requests are
/// the ones <see cref="SmbTestExchange"/> builds from the pinned upstream curl build's recording.
/// </summary>
[TestClass]
public sealed class SmbCommandFaultsTests
{
    private static readonly byte[] Upload = "upload me\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Create_UnderARule_IsTheRulesNtStatusAndTheStoreIsUnchanged_WhileAnotherFileOpens()
    {
        var fileSystem = StandardFileSystem();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "smb", Command = "CREATE", Path = "/share/up.txt", Reply = "C0000022" });

        var run = await ServeConnectedAsync(UploadContentStore(fileSystem), plan, UploadNtCreateHex("up.txt"), UploadNtCreateHex("other.txt"));

        Assert.StartsWith(FaultErrorHex(SmbCommand.NtCreateAndX, 0xC0000022) + UploadNtCreateResponseHex(1, 2), run.Written);
        Assert.IsNull(StoredBytes(fileSystem, "share", "up.txt"));
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "/share/up.txt"), run.Events.Single());
        CollectionAssert.Contains(run.Notes, "SMB CREATE /share/up.txt refused by fault rule f1: 0xC0000022");
        Assert.AreEqual(
            new FaultPoint(FaultStage.Command) { ExchangeId = 1, ListenUrl = "smb://127.0.0.1:445/", Scheme = "smb", Account = "alice", Path = "/share/up.txt", Command = "CREATE" },
            plan.Points[1]);
    }

    [TestMethod]
    public async Task Read_UnderARuleForTheOpenFilesPath_IsTheRulesNtStatus()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "READ", Path = "/share/file.txt", Reply = "c000000e" });

        var run = await ServeConnectedAsync(StandardContentStore(), plan, NtCreateHex(1, "file.txt"), ReadHex(1, 1, 0));

        Assert.EndsWith(FaultErrorHex(SmbCommand.ReadAndX, 0xC000000E), run.Written);
        Assert.AreEqual("/share/file.txt", ((FaultAppliedEvent)run.Events.Single()).Target);
    }

    [TestMethod]
    public async Task EveryCommandAfterTheLogin_IsAskedAboutWithItsNameAndPath()
    {
        var plan = new ScriptedFaultPlan();

        await ServeConnectedAsync(
            UploadContentStore(StandardFileSystem()),
            plan,
            NtCreateHex(1, "file.txt"),
            ReadHex(1, 1, 0),
            CloseHex(1, 1),
            UploadNtCreateHex(@"sub\up.txt"),
            WriteHex(1, 0, Upload),
            CloseHex(1, 1),
            ReadHex(1, 9, 0),
            NtCreateHex(7, "file.txt"),
            TreeDisconnectHex(7),
            TreeDisconnectHex(1));

        CollectionAssert.AreEqual(
            new[]
            {
                "TREE_CONNECT /share", "CREATE /share/file.txt", "READ /share/file.txt", "CLOSE /share/file.txt",
                "CREATE /share/sub/up.txt", "WRITE /share/sub/up.txt", "CLOSE /share/sub/up.txt", "READ ", "CREATE ",
                "TREE_DISCONNECT ", "TREE_DISCONNECT /share",
            },
            plan.Points.Select(point => $"{point.Command} {point.Path}").ToArray());
    }

    [TestMethod]
    public async Task Rule_ForACommandNamingNoPath_IsNotedWithoutOne()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Command = "CREATE", Reply = "C0000001" });

        var run = await ServeConnectedAsync(StandardContentStore(), plan, NtCreateHex(7, "file.txt"));

        Assert.AreEqual(FaultErrorHex(SmbCommand.NtCreateAndX, 0xC0000001, treeId: 7), run.Written);
        CollectionAssert.Contains(run.Notes, "SMB CREATE (no path) refused by fault rule f1: 0xC0000001");
        Assert.AreEqual("CREATE", ((FaultAppliedEvent)run.Events.Single()).Target);
    }

    [TestMethod]
    [DataRow(FaultKind.Delay)]
    [DataRow(FaultKind.Drop)]
    [DataRow(FaultKind.OutOfSpace)]
    public async Task Rule_OfAnotherKind_IsNotAppliedAtACommand(FaultKind kind)
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", kind) { Reply = "C0000022" });

        var run = await ServeConnectedAsync(StandardContentStore(), plan, NtCreateHex(1, "file.txt"));

        Assert.AreEqual(NtCreateResponseHex(1, 10), run.Written);
        Assert.IsEmpty(run.Events);
        CollectionAssert.Contains(run.Notes, $"Fault rule f1 not applied to SMB CREATE: a {kind} rule is not applied at a command");
    }

    [TestMethod]
    public async Task Rule_WhoseReplyDoesNotFit_IsNotAppliedAndIsNoted()
    {
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Reply = "00000000" });

        var run = await ServeConnectedAsync(StandardContentStore(), plan, NtCreateHex(1, "file.txt"));

        Assert.AreEqual(NtCreateResponseHex(1, 10), run.Written);
        Assert.IsEmpty(run.Events);
        CollectionAssert.Contains(run.Notes, "Fault rule f1 not applied to SMB CREATE: its reply does not fit the smb error shape");
    }

    [TestMethod]
    [DataRow("C0000022", true, 0xC0000022u)]
    [DataRow("c000000f", true, 0xC000000Fu)]
    [DataRow("80000005", false, 0u)]
    [DataRow("C00000", false, 0u)]
    [DataRow("C000002Z", false, 0u)]
    [DataRow(null, false, 0u)]
    public void TryReadNtStatus_ReadsAnErrorAsEightHexDigits(string? reply, bool fits, uint status)
    {
        var read = SmbCommandFaults.TryReadNtStatus(reply, out var readStatus);

        Assert.AreEqual((fits, status), (read, readStatus));
    }

    [TestMethod]
    [DataRow("fe80::1%4", 445, "smb://[fe80::1%254]:445/")]
    [DataRow("localhost", 4450, "smb://localhost:4450/")]
    public void FormatListenUrl_NamesTheListenUrlAsTheEngineDoes(string host, int port, string expected) =>
        Assert.AreEqual(expected, SmbCommandFaults.FormatListenUrl(new ListenUrl("smb", host, port)));

    // An error response whose Flags2 has SMB_FLAGS2_NT_STATUS (0x4000) beside curl's 0x0041.
    private static string FaultErrorHex(byte command, uint status, ushort treeId = 1) =>
        ErrorHex(command, status, treeId).Replace("984100", "984140", StringComparison.Ordinal);

    private async Task<FaultRun> ServeConnectedAsync(ContentStore contentStore, IFaultPlan plan, params string[] requestsHex)
    {
        var log = new RecordingExchangeLog();
        var events = new RecordingSurlEventPublisher();
        var connection = new InMemoryConnection([Hex(string.Concat([NegotiateHex, SessionSetupHex, TreeConnectHex("share"), .. requestsHex]))]);
        var context = Context(new ManualTimeProvider(), TestContext.CancellationToken, log: log) with { Faults = plan, Events = events };

        await Server(contentStore: contentStore).ServeAsync(connection, context);

        var written = Convert.ToHexString(connection.WrittenBytes);
        var prefix = NegotiateResponseHex + SessionSetupResponseHex + TreeConnectResponseHex(1);
        Assert.StartsWith(prefix, written);
        return new FaultRun(written[prefix.Length..], [.. log.Notes], [.. events.Events]);
    }

    private sealed record FaultRun(string Written, string[] Notes, SurlEvent[] Events);
}
