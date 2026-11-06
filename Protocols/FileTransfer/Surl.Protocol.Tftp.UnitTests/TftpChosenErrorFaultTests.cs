using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// The chosen-error fault (ADR-0124, decision 4): a read or write request a matching
/// <see cref="FaultKind.Error"/> rule names is answered from the transfer port with the ERROR
/// packet carrying the rule's code and text, before the content store is touched, and publishes
/// one <see cref="FaultAppliedEvent"/>. The recorded case (Fixtures/README.md, <c>fault-read</c>)
/// is what pinned upstream curl 8.21.0 sent and how it exited when its server answered so.
/// </summary>
[TestClass]
public sealed class TftpChosenErrorFaultTests
{
    private static readonly string Root = Path.Join(Path.GetTempPath(), "surl-tftp-fault-tests");
    private static readonly DateTimeOffset FileTime = new(2026, 11, 5, 8, 30, 0, TimeSpan.Zero);
    private static readonly byte[] FileBody = Encoding.ASCII.GetBytes("Hello from Surl.\n");

    [TestMethod]
    public async Task ServeAsync_RecordedReadUnderARule_SendsTheRulesErrorFromTheTransferPort()
    {
        var curl = RecordedFixture.CurlDatagrams("fault-read");
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Scheme = "tftp", Command = "RRQ", Path = "/up/a.txt", Reply = "2 Quota frozen for the test" });

        var flow = await ServeAsync(curl[0], plan, events, StandardFileSystem());

        CollectionAssert.AreEqual(RecordedFixture.ServerDatagrams("fault-read").Single(), flow.Sent.Single().Bytes);
        Assert.AreEqual(ScriptedDatagramFlow.TransferPort, flow.Sent.Single().FromPort);
        Assert.AreEqual(new FaultAppliedEvent("f1", FaultKind.Error, FaultStage.Command, 1, "RRQ /up/a.txt"), events.Events.Single());
        Assert.AreEqual("69", Encoding.ASCII.GetString(RecordedFixture.ReadBytes("fault-read", "exitcode.txt")));
    }

    [TestMethod]
    public async Task ServeAsync_WriteUnderARule_SendsTheRulesErrorAndWritesNothing()
    {
        var fileSystem = StandardFileSystem();
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f2", FaultKind.Error) { Command = "WRQ", Reply = "3 Disk full for the test" });

        var flow = await ServeAsync(Request(isWrite: true, "up.txt"), plan, events, fileSystem, allowUploads: true);

        CollectionAssert.AreEqual(ErrorDatagram(3, "Disk full for the test"), flow.Sent.Single().Bytes);
        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(Path.Join(Root, "up.txt")));
        Assert.AreEqual(new FaultAppliedEvent("f2", FaultKind.Error, FaultStage.Command, 1, "WRQ /up.txt"), events.Events.Single());
    }

    [TestMethod]
    public async Task ServeAsync_ReadOutsideTheRule_IsServedAndAskedAboutWithItsPoint()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.Error) { Path = "/other.txt", Reply = "0 Not now" });

        var flow = await ServeAsync(Request(isWrite: false, "/file name.txt"), plan, events, StandardFileSystem(), finalAck: true);

        byte[] firstBlock = [0, 3, 0, 1, .. FileBody];
        CollectionAssert.AreEqual(firstBlock, flow.Sent.Single().Bytes);
        Assert.IsEmpty(events.Events);
        Assert.AreEqual(new FaultPoint(FaultStage.Command)
        {
            ExchangeId = 1,
            ListenUrl = $"tftp://127.0.0.1:{ScriptedDatagramFlow.ListenPort}/",
            Scheme = "tftp",
            Path = "/file name.txt",
            Command = "RRQ",
        }, plan.Points.Single());
    }

    [TestMethod]
    [DataRow(FaultKind.Delay, "1 Gone", DisplayName = "a rule of another kind")]
    [DataRow(FaultKind.Error, null, DisplayName = "an error rule without a reply")]
    public async Task ServeAsync_ReadMatchedByARuleWithNoChosenError_IsServed(FaultKind kind, string? reply)
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", kind) { Reply = reply, DelayMilliseconds = 0 });

        var flow = await ServeAsync(Request(isWrite: false, "/file name.txt"), plan, events, StandardFileSystem(), finalAck: true);

        Assert.AreEqual(TftpPacket.Data, flow.Sent.Single().Bytes[1]);
        Assert.IsEmpty(events.Events);
    }

    [TestMethod]
    [DataRow("2 Quota frozen", 2, "Quota frozen", DisplayName = "a code and its text")]
    [DataRow("0 Busy, try later", 0, "Busy, try later", DisplayName = "code 0, text with spaces")]
    [DataRow("7", 7, "", DisplayName = "a code without text")]
    [DataRow("8 Too high", 0, "8 Too high", DisplayName = "a code past 7 is sent whole as ERROR 0")]
    [DataRow("busy", 0, "busy", DisplayName = "no code is sent whole as ERROR 0")]
    public void ErrorPacket_Reply_CarriesItsCodeAndText(string reply, int expectedCode, string expectedMessage)
    {
        var packet = TftpChosenErrorFault.ErrorPacket(reply);

        CollectionAssert.AreEqual(ErrorDatagram(expectedCode, expectedMessage), packet);
    }

    private static byte[] ErrorDatagram(int code, string message) => [0, 5, 0, (byte)code, .. Encoding.ASCII.GetBytes(message), 0];

    private static byte[] Request(bool isWrite, string fileName) =>
        [0, isWrite ? (byte)2 : (byte)1, .. Encoding.ASCII.GetBytes(fileName + "\0octet\0")];

    private static UnitTestInMemoryContentFileSystem StandardFileSystem() => new UnitTestInMemoryContentFileSystem()
        .AddDirectory(Root)
        .AddDirectory(Path.Join(Root, "up"))
        .AddFile(Path.Join(Root, "up", "a.txt"), FileBody, FileTime)
        .AddFile(Path.Join(Root, "file name.txt"), FileBody, FileTime);

    private static async Task<ScriptedDatagramFlow> ServeAsync(
        byte[] firstDatagram, IFaultPlan plan, ISurlEventPublisher events, UnitTestInMemoryContentFileSystem fileSystem, bool allowUploads = false, bool finalAck = false)
    {
        var clock = new ManualTimeProvider();
        var script = finalAck ? new[] { ScriptedDatagramFlow.Datagram([0, 4, 0, 1]) } : [];
        var flow = new ScriptedDatagramFlow(firstDatagram, clock, script);
        var context = new ExchangeContext(
            1,
            new ListenUrl("tftp", "127.0.0.1", ScriptedDatagramFlow.ListenPort).WithBoundPort(ScriptedDatagramFlow.ListenPort),
            new IPEndPoint(IPAddress.Loopback, ScriptedDatagramFlow.ListenPort),
            new IPEndPoint(IPAddress.Loopback, 50000),
            new RecordingExchangeLog(),
            clock,
            CancellationToken.None)
        {
            Faults = plan,
            Events = events,
        };
        var store = new ContentStore(Root, fileSystem, new ContentExposureOptions { AllowUploads = allowUploads });

        await new TftpProtocolServer(store).ServeAsync(flow, context);

        return flow;
    }
}
