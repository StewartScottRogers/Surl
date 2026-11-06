using Surl.Protocol.Abstractions;
using static Surl.Protocol.Tftp.UploadExchange;

namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class WriteRequestTests
{
    private static readonly byte[] FullBlock = new byte[512];

    [TestMethod]
    public async Task Wrq_WithUploadsOff_AnswersError2AndCreatesNoFile()
    {
        var fileSystem = FileSystem();
        var (flow, log) = await ReplayAsync(Store(fileSystem, allowUploads: false), "write-refused-uploads-off");

        AssertSent(RecordedFixture.ServerDatagrams("write-refused-uploads-off"), flow);
        Assert.AreEqual(ScriptedDatagramFlow.TransferPort, flow.Sent[0].FromPort);
        Assert.IsNull(fileSystem.ReadFile(UploadPath));
        Assert.AreEqual("Write of \"up.txt\" (octet): refused, because uploads are off; answered with ERROR 2.", log.Notes.Single());
    }

    [TestMethod]
    [DataRow("write-accepted")]
    [DataRow("write-accepted-no-options")]
    public async Task Wrq_WithUploadsOn_WritesTheFile(string caseName)
    {
        var fileSystem = FileSystem();
        var (flow, log) = await ReplayAsync(Store(fileSystem), caseName);

        AssertSent(RecordedFixture.ServerDatagrams(caseName), flow);
        CollectionAssert.AreEqual(RecordedFixture.ReadBytes(caseName, "upload.bin"), fileSystem.ReadFile(UploadPath));
        Assert.AreEqual($"Write of \"up.txt\" (octet): 2 blocks were written to {UploadPath} and acknowledged.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task Wrq_WholeTransfer_IsServedOnTheOneFlowItWasGiven()
    {
        var fileSystem = FileSystem();
        var (flow, _) = await ReplayAsync(Store(fileSystem), "write-accepted");

        Assert.HasCount(3, flow.Sent);
        Assert.IsTrue(flow.Sent.All(sent => sent.FromPort == ScriptedDatagramFlow.TransferPort));
        Assert.AreEqual(0, flow.RemainingSteps);
        Assert.IsNotNull(fileSystem.ReadFile(UploadPath));
    }

    [TestMethod]
    public async Task Wrq_RefusedName_AnswersError2()
    {
        var (flow, log) = await ServeAsync(Store(FileSystem()), WriteRequest("../up.txt"), []);

        CollectionAssert.AreEqual(AccessViolation, flow.Sent.Single().Bytes);
        Assert.StartsWith("Write of \"../up.txt\" (octet): refused by the content store (", log.Notes.Single());
    }

    [TestMethod]
    [DataRow("nodir/up.txt")]
    [DataRow(".hidden")]
    public async Task Wrq_WhereTheStoreDoesNotPermitAnUpload_AnswersError2BeforeAnyAck(string fileName)
    {
        var fileSystem = FileSystem();
        var (flow, log) = await ServeAsync(Store(fileSystem), WriteRequest(fileName, "tsize", "6"), []);

        CollectionAssert.AreEqual(AccessViolation, flow.Sent.Single().Bytes);
        Assert.EndsWith("; answered with ERROR 2.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task Wrq_FileOfExactlyOneBlock_EndsWithTheEmptyBlock()
    {
        var fileSystem = FileSystem();
        var (flow, _) = await ServeAsync(
            Store(fileSystem),
            WriteRequest("up.txt"),
            [ScriptedDatagramFlow.Datagram(Data(1, FullBlock)), ScriptedDatagramFlow.Datagram(Data(2, []))]);

        AssertSent([Ack(0), Ack(1), Ack(2)], flow);
        CollectionAssert.AreEqual(FullBlock, fileSystem.ReadFile(UploadPath));
    }

    [TestMethod]
    public async Task Wrq_DuplicateData_IsIgnoredAndNeverAcknowledgedTwice()
    {
        var (flow, _) = await ServeAsync(
            Store(FileSystem()),
            WriteRequest("up.txt"),
            [
                ScriptedDatagramFlow.Datagram(Data(1, FullBlock)),
                ScriptedDatagramFlow.Datagram(Data(1, FullBlock)),
                ScriptedDatagramFlow.Datagram(Data(2, "end"u8.ToArray())),
            ]);

        AssertSent([Ack(0), Ack(1), Ack(2)], flow);
    }

    [TestMethod]
    public async Task Wrq_DataLostForOneTimeout_SendsTheAckAgain()
    {
        var (flow, _) = await ServeAsync(
            Store(FileSystem()),
            WriteRequest("up.txt"),
            [ScriptedDatagramFlow.Silence(TimeSpan.FromSeconds(5)), ScriptedDatagramFlow.Datagram(Data(1, "hi"u8.ToArray()))]);

        AssertSent([Ack(0), Ack(0), Ack(1)], flow);
    }

    [TestMethod]
    public async Task Wrq_ClientSilent_GivesUpAndLeavesNoFile()
    {
        var fileSystem = FileSystem();
        var (flow, log) = await ServeAsync(Store(fileSystem), WriteRequest("up.txt"), []);

        Assert.HasCount(TftpLockStep.MaximumRetransmissions + 1, flow.Sent);
        Assert.IsNull(fileSystem.ReadFile(UploadPath));
        CollectionAssert.AreEqual(
            new[]
            {
                $"Write of \"up.txt\" (octet): receiving into {UploadPath} in blocks of 512",
                "Nothing came while DATA 1 was awaited, through 5 retransmissions 5 seconds apart; the transfer was abandoned.",
                "Write of \"up.txt\" (octet): the transfer ended after 0 blocks; the partial file was deleted.",
            },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task Wrq_ClientSendsError_EndsWithNoReplyAndLeavesNoFile()
    {
        var fileSystem = FileSystem();
        var (flow, log) = await ServeAsync(
            Store(fileSystem),
            WriteRequest("up.txt"),
            [ScriptedDatagramFlow.Datagram(Data(1, FullBlock)), ScriptedDatagramFlow.Datagram([0, 5, 0, 0, 0])]);

        AssertSent([Ack(0), Ack(1)], flow);
        Assert.IsNull(fileSystem.ReadFile(UploadPath));
        Assert.AreEqual("The client ended the transfer with an ERROR while DATA 2 was awaited.", log.Notes[^2]);
        Assert.AreEqual("Write of \"up.txt\" (octet): the transfer ended after 1 blocks; the partial file was deleted.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task Wrq_ClientSendsAnAck_EndsWithError4AndLeavesNoFile()
    {
        var fileSystem = FileSystem();
        var (flow, _) = await ServeAsync(Store(fileSystem), WriteRequest("up.txt"), [ScriptedDatagramFlow.Datagram(Ack(1))]);

        AssertSent([Ack(0), IllegalOperation], flow);
        Assert.IsNull(fileSystem.ReadFile(UploadPath));
    }

    [TestMethod]
    public async Task Wrq_DiskWriteFails_AnswersError3WithTheFixedTextAndLeavesNoFile()
    {
        var fileSystem = new UnitTestInMemoryContentFileSystem { FailWrites = true }.AddDirectory(Root);
        var (flow, log) = await ServeAsync(Store(fileSystem), WriteRequest("up.txt"), [ScriptedDatagramFlow.Datagram(Data(1, "hi"u8.ToArray()))]);

        AssertSent([Ack(0), RecordedFixture.ServerDatagrams("write-too-large").Single()], flow);
        Assert.IsNull(fileSystem.ReadFile(UploadPath));
        Assert.AreEqual($"Write of \"up.txt\" (octet): writing {UploadPath} failed in block 1; the partial file was deleted and the transfer ended with ERROR 3.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task Wrq_ExchangeCancelled_ThrowsAndLeavesNoFile()
    {
        var fileSystem = FileSystem();
        var clock = new ManualTimeProvider();
        using var cancellation = new CancellationTokenSource();
        var flow = new ScriptedDatagramFlow(WriteRequest("up.txt"), clock, [ScriptedDatagramFlow.Datagram(Data(1, FullBlock))]);
        var server = new TftpProtocolServer(Store(fileSystem));
        var context = Context(new RecordingExchangeLog(), clock, cancellationToken: cancellation.Token);
        var serving = server.ServeAsync(new CancelOnSecondAckFlow(flow, cancellation), context);

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsNull(fileSystem.ReadFile(UploadPath));
    }

    /// <summary>
    /// A flow that cancels the exchange when the server sends its second datagram.
    /// </summary>
    private sealed class CancelOnSecondAckFlow(ScriptedDatagramFlow inner, CancellationTokenSource exchange) : IDatagramFlow
    {
        public System.Net.EndPoint LocalEndPoint => inner.LocalEndPoint;

        public System.Net.EndPoint RemoteEndPoint => inner.RemoteEndPoint;

        public ReadOnlyMemory<byte> FirstDatagram => inner.FirstDatagram;

        public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) => inner.ReceiveAsync(cancellationToken);

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
        {
            if (inner.Sent.Count == 1)
            {
                exchange.Cancel();
            }

            return inner.SendAsync(datagram, cancellationToken);
        }

        public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) => inner.MoveToNewLocalPortAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
