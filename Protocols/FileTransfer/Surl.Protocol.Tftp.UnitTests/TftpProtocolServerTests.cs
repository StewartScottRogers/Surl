using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class TftpProtocolServerTests
{
    private static readonly string Root = Path.Join(Path.GetTempPath(), "surl-tftp-tests");
    private static readonly DateTimeOffset FileTime = new(2026, 9, 1, 8, 30, 0, TimeSpan.Zero);
    private static readonly byte[] FileBody = Encoding.ASCII.GetBytes("Hello from Surl.\n");
    private static readonly byte[] FileNotFound = [0, 5, 0, 1, .. "File not found"u8, 0];
    private static readonly byte[] IllegalOperation = [0, 5, 0, 4, .. "Illegal TFTP operation"u8, 0];
    private static readonly byte[] ChangedFileError = [0, 5, 0, 0, .. "The file changed while it was sent."u8, 0];

    [TestMethod]
    public void Constructor_NullContentStore_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TftpProtocolServer(null!));
    }

    [TestMethod]
    public void Schemes_IsTftpOnly()
    {
        var server = new TftpProtocolServer(new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot));

        CollectionAssert.AreEqual(new[] { "tftp" }, server.Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new TftpProtocolServer(new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot));
        var clock = new ManualTimeProvider();
        var flow = new ScriptedDatagramFlow(ReadRequest("file.txt"), clock, []);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(new RecordingExchangeLog(), clock)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(flow, null!));
    }

    [TestMethod]
    [DataRow("default-read")]
    [DataRow("blksize-1024-read")]
    [DataRow("no-options-read")]
    [DataRow("missing-file")]
    [DataRow("exactly-512-bytes")]
    [DataRow("write-refused")]
    public async Task ServeAsync_RecordedExchange_SendsTheDatagramsCurlAcceptedFromTheTransferPort(string caseName)
    {
        var curl = RecordedFixture.CurlDatagrams(caseName);
        var (flow, _) = await ServeAsync(curl[0], curl.Skip(1).Select(ScriptedDatagramFlow.Datagram));

        var expected = RecordedFixture.ServerDatagrams(caseName);
        Assert.HasCount(expected.Count, flow.Sent);
        for (var index = 0; index < expected.Count; index++)
        {
            CollectionAssert.AreEqual(expected[index], flow.Sent[index].Bytes, $"datagram {index}");
        }

        Assert.IsTrue(flow.Sent.All(sent => sent.FromPort == ScriptedDatagramFlow.TransferPort));
        Assert.AreEqual(0, flow.RemainingSteps);
    }

    [TestMethod]
    [DataRow("default-read")]
    [DataRow("blksize-1024-read")]
    [DataRow("no-options-read")]
    [DataRow("exactly-512-bytes")]
    public async Task ServeAsync_RecordedRead_SendsTheBytesCurlWroteToStdout(string caseName)
    {
        var curl = RecordedFixture.CurlDatagrams(caseName);
        var (flow, _) = await ServeAsync(curl[0], curl.Skip(1).Select(ScriptedDatagramFlow.Datagram));

        var dataPayloads = flow.SentBytes.Where(sent => sent[1] == TftpPacket.Data).SelectMany(sent => sent.Skip(4)).ToArray();
        CollectionAssert.AreEqual(RecordedFixture.ReadBytes(caseName, "stdout.bin"), dataPayloads);
    }

    [TestMethod]
    public async Task ServeAsync_DefaultRead_OpensWithTheOptionAcknowledgementAndLogsTheTransfer()
    {
        var curl = RecordedFixture.CurlDatagrams("default-read");
        var (flow, log) = await ServeAsync(curl[0], curl.Skip(1).Select(ScriptedDatagramFlow.Datagram));

        Assert.AreEqual("\0\u0006tsize\u000017\0blksize\0512\0timeout\u00006\0", Encoding.Latin1.GetString(flow.Sent[0].Bytes));
        CollectionAssert.AreEqual(
            new[]
            {
                $"Read of \"file.txt\" (octet): 17 bytes of {Path.Join(Root, "file.txt")} in blocks of 512",
                $"All 17 bytes of {Path.Join(Root, "file.txt")} were acknowledged in 1 blocks.",
            },
            log.Notes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_ExactlyOneBlock_EndsWithAnEmptyDataPacket()
    {
        var curl = RecordedFixture.CurlDatagrams("exactly-512-bytes");
        var (flow, log) = await ServeAsync(curl[0], curl.Skip(1).Select(ScriptedDatagramFlow.Datagram));

        CollectionAssert.AreEqual(new byte[] { 0, 3, 0, 2 }, flow.Sent[^1].Bytes);
        Assert.Contains("were acknowledged in 2 blocks.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_AckLostForOneTimeout_SendsTheBlockAgain()
    {
        var (flow, _) = await ServeAsync(
            ReadRequest("file.txt"),
            [ScriptedDatagramFlow.Silence(TimeSpan.FromSeconds(5)), ScriptedDatagramFlow.Datagram(Ack(1))]);

        var data = Data(1, FileBody);
        Assert.HasCount(2, flow.Sent);
        CollectionAssert.AreEqual(data, flow.Sent[0].Bytes);
        CollectionAssert.AreEqual(data, flow.Sent[1].Bytes);
    }

    [TestMethod]
    public async Task ServeAsync_AckJustBeforeTheTimeout_SendsTheBlockOnce()
    {
        var (flow, _) = await ServeAsync(
            ReadRequest("file.txt"),
            [ScriptedDatagramFlow.Silence(TimeSpan.FromSeconds(5) - TimeSpan.FromTicks(1)), ScriptedDatagramFlow.Datagram(Ack(1))]);

        Assert.HasCount(1, flow.Sent);
    }

    [TestMethod]
    public async Task ServeAsync_NegotiatedTimeout_IsTheRetransmissionTimeout()
    {
        var (flow, _) = await ServeAsync(
            ReadRequest("file.txt", "timeout", "2"),
            [
                ScriptedDatagramFlow.Datagram(Ack(0)),
                ScriptedDatagramFlow.Silence(TimeSpan.FromSeconds(2)),
                ScriptedDatagramFlow.Datagram(Ack(1)),
            ]);

        Assert.HasCount(3, flow.Sent);
        CollectionAssert.AreEqual(flow.Sent[1].Bytes, flow.Sent[2].Bytes);
    }

    [TestMethod]
    public async Task ServeAsync_ClientSilent_GivesUpAfterTheRetryCount()
    {
        var (flow, log) = await ServeAsync(ReadRequest("file.txt"), []);

        Assert.HasCount(TftpLockStep.MaximumRetransmissions + 1, flow.Sent);
        Assert.IsTrue(flow.SentBytes.All(sent => sent.SequenceEqual(Data(1, FileBody))));
        Assert.AreEqual("Nothing came while block 1 awaited its ACK, through 5 retransmissions 5 seconds apart; the transfer was abandoned.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_OptionAcknowledgementNeverAcknowledged_GivesUpWithoutData()
    {
        var (flow, _) = await ServeAsync(ReadRequest("file.txt", "tsize", "0"), []);

        Assert.HasCount(TftpLockStep.MaximumRetransmissions + 1, flow.Sent);
        Assert.IsTrue(flow.SentBytes.All(sent => sent[1] == TftpPacket.OptionAcknowledgement));
    }

    [TestMethod]
    public async Task ServeAsync_DuplicateAck_IsIgnoredAndNeverResendsABlock()
    {
        var curl = RecordedFixture.CurlDatagrams("blksize-1024-read");
        var (flow, _) = await ServeAsync(
            curl[0],
            [
                ScriptedDatagramFlow.Datagram(curl[1]),
                ScriptedDatagramFlow.Datagram(curl[2]),
                ScriptedDatagramFlow.Datagram(curl[2]),
                ScriptedDatagramFlow.Datagram(Ack(0)),
                ScriptedDatagramFlow.Datagram(curl[3]),
            ]);

        var expected = RecordedFixture.ServerDatagrams("blksize-1024-read");
        Assert.HasCount(expected.Count, flow.Sent);
        CollectionAssert.AreEqual(expected[^1], flow.Sent[^1].Bytes);
    }

    [TestMethod]
    public async Task ServeAsync_ClientSendsError_EndsTheTransferWithNoReply()
    {
        var (flow, log) = await ServeAsync(ReadRequest("file.txt"), [ScriptedDatagramFlow.Datagram([0, 5, 0, 0, 0])]);

        Assert.HasCount(1, flow.Sent);
        Assert.AreEqual("The client ended the transfer with an ERROR while block 1 awaited its ACK.", log.Notes[^1]);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 3, 0, 1, 65 })]
    [DataRow(new byte[] { 0, 4, 0 })]
    [DataRow(new byte[] { 9 })]
    public async Task ServeAsync_ClientSendsAnIllegalPacket_EndsTheTransferWithError4(byte[] packet)
    {
        var (flow, _) = await ServeAsync(ReadRequest("file.txt"), [ScriptedDatagramFlow.Datagram(packet)]);

        Assert.HasCount(2, flow.Sent);
        CollectionAssert.AreEqual(IllegalOperation, flow.Sent[1].Bytes);
    }

    [TestMethod]
    public async Task ServeAsync_WriteRequest_IsRefusedWithAccessViolation()
    {
        var (flow, log) = await ServeAsync([0, 2, .. "upload.txt"u8, 0, .. "octet"u8, 0], []);

        CollectionAssert.AreEqual(RecordedFixture.ServerDatagrams("write-refused")[0], flow.Sent.Single().Bytes);
        Assert.AreEqual(ScriptedDatagramFlow.TransferPort, flow.Sent[0].FromPort);
        Assert.AreEqual("Write of \"upload.txt\" (octet): refused, because uploads are off; answered with ERROR 2.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_FirstDatagramIsAnError_SendsNothing()
    {
        var (flow, log) = await ServeAsync([0, 5, 0, 0, 0], []);

        Assert.IsEmpty(flow.Sent);
        Assert.AreEqual(ScriptedDatagramFlow.ListenPort, ((IPEndPoint)flow.LocalEndPoint).Port);
        Assert.AreEqual("The flow opened with an ERROR packet; it was not answered.", log.Notes.Single());
    }

    [TestMethod]
    [DataRow(new byte[] { 1 })]
    [DataRow(new byte[] { 0, 9, 0 })]
    [DataRow(new byte[] { 0, 1, 102, 0, 111, 99, 116, 101, 116 })]
    [DataRow(new byte[] { 0, 1, 102, 0, 109, 97, 105, 108, 0 })]
    [DataRow(new byte[] { 0, 1, 102, 0, 111, 99, 116, 101, 116, 0, 116, 115, 105, 122, 101, 0 })]
    public async Task ServeAsync_MalformedFirstDatagram_IsAnsweredWithError4(byte[] datagram)
    {
        var (flow, log) = await ServeAsync(datagram, []);

        CollectionAssert.AreEqual(IllegalOperation, flow.Sent.Single().Bytes);
        Assert.AreEqual(ScriptedDatagramFlow.TransferPort, flow.Sent[0].FromPort);
        Assert.AreEqual("The first datagram is not a well-formed read or write request; answered with ERROR 4.", log.Notes.Single());
    }

    [TestMethod]
    [DataRow("sub")]
    [DataRow("")]
    [DataRow("missing.txt")]
    [DataRow("../file.txt")]
    public async Task ServeAsync_NoFileAtTheName_IsAnsweredWithFileNotFound(string fileName)
    {
        var (flow, _) = await ServeAsync(ReadRequest(fileName, "tsize", "0"), []);

        CollectionAssert.AreEqual(FileNotFound, flow.Sent.Single().Bytes);
    }

    [TestMethod]
    public async Task ServeAsync_RefusedName_LogsTheRefusal()
    {
        var (_, log) = await ServeAsync(ReadRequest("../file.txt"), []);

        Assert.StartsWith("Read of \"../file.txt\" (octet): refused by the content store (", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_FileVanishesBeforeItIsRead_IsAnsweredWithFileNotFound()
    {
        var (flow, log) = await ServeAsync(ReadRequest("file.txt"), [], new UnitTestVanishingContentFileSystem(StandardFileSystem()));

        CollectionAssert.AreEqual(FileNotFound, flow.Sent.Single().Bytes);
        Assert.AreEqual($"Read of \"file.txt\" (octet): no file at {Path.Join(Root, "file.txt")}; answered with ERROR 1.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_FileShrinksWhileItIsSent_EndsWithError0()
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "shrinking.bin"), new byte[600], FileTime, reportedLength: 1000);
        var (flow, log) = await ServeAsync(ReadRequest("shrinking.bin"), [ScriptedDatagramFlow.Datagram(Ack(1))], fileSystem);

        Assert.HasCount(2, flow.Sent);
        CollectionAssert.AreEqual(ChangedFileError, flow.Sent[1].Bytes);
        Assert.Contains("shrank below 1000 bytes", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_NetasciiMode_SendsTheBytesUnchanged()
    {
        var body = "a\r\nb\r\0c"u8.ToArray();
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "t.txt"), body, FileTime);
        byte[] request = [0, 1, .. "t.txt"u8, 0, .. "NetASCII"u8, 0];
        var (flow, log) = await ServeAsync(request, [ScriptedDatagramFlow.Datagram(Ack(1))], fileSystem);

        CollectionAssert.AreEqual(Data(1, body), flow.Sent.Single().Bytes);
        Assert.StartsWith("Read of \"t.txt\" (netascii)", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_EncodedFileName_MapsTheDecodedBytesCurlSends()
    {
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "sub", "a b%cé.txt"), FileBody, FileTime);
        byte[] request = [0, 1, .. "sub/a b%c"u8, 0xC3, 0xA9, .. ".txt"u8, 0, .. "octet"u8, 0];
        var (flow, log) = await ServeAsync(request, [ScriptedDatagramFlow.Datagram(Ack(1))], fileSystem);

        CollectionAssert.AreEqual(Data(1, FileBody), flow.Sent.Single().Bytes);
        Assert.StartsWith(@"Read of ""sub/a b%c\xC3\xA9.txt"" (octet): 17 bytes", log.Notes[0]);
    }

    [TestMethod]
    public async Task ServeAsync_MoreThan65535Blocks_WrapsTheBlockNumberToZero()
    {
        const int BlockSize = 8;
        var body = new byte[65536 * BlockSize];
        body[^1] = 42;
        var fileSystem = StandardFileSystem().AddFile(Path.Join(Root, "large.bin"), body, FileTime);
        var acks = new List<ScriptedDatagramFlow.Step> { ScriptedDatagramFlow.Datagram(Ack(0)) };
        acks.AddRange(Enumerable.Range(1, 65537).Select(block => ScriptedDatagramFlow.Datagram(Ack((ushort)block))));

        var (flow, log) = await ServeAsync(ReadRequest("large.bin", "blksize", "8"), acks, fileSystem);

        Assert.HasCount(65538, flow.Sent);
        CollectionAssert.AreEqual(new byte[] { 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 42 }, flow.Sent[^2].Bytes);
        CollectionAssert.AreEqual(new byte[] { 0, 3, 0, 1 }, flow.Sent[^1].Bytes);
        Assert.AreEqual($"All 524288 bytes of {Path.Join(Root, "large.bin")} were acknowledged in 65537 blocks.", log.Notes[^1]);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelled_Throws()
    {
        var clock = new ManualTimeProvider();
        var server = new TftpProtocolServer(new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot));
        var flow = new ScriptedDatagramFlow(ReadRequest("file.txt"), clock, []);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => server.ServeAsync(flow, Context(new RecordingExchangeLog(), clock, cancellation.Token)));
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledWhileAwaitingAnAck_Throws()
    {
        var clock = new ManualTimeProvider();
        var server = new TftpProtocolServer(new ContentStore(Root, StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot));
        using var cancellation = new CancellationTokenSource();
        var flow = new CancellingDatagramFlow(new ScriptedDatagramFlow(ReadRequest("file.txt"), clock, []), cancellation);

        await Assert.ThrowsAsync<OperationCanceledException>(() => server.ServeAsync(flow, Context(new RecordingExchangeLog(), clock, cancellation.Token)));
    }

    internal static byte[] ReadRequest(string fileName, params string[] options)
    {
        var text = new StringBuilder(fileName).Append('\0').Append("octet").Append('\0');
        foreach (var field in options)
        {
            text.Append(field).Append('\0');
        }

        return [0, 1, .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static byte[] Ack(ushort blockNumber) => [0, 4, (byte)(blockNumber >> 8), (byte)blockNumber];

    private static byte[] Data(ushort blockNumber, byte[] payload) => [0, 3, (byte)(blockNumber >> 8), (byte)blockNumber, .. payload];

    private static UnitTestInMemoryContentFileSystem StandardFileSystem() => new UnitTestInMemoryContentFileSystem()
        .AddDirectory(Root)
        .AddDirectory(Path.Join(Root, "sub"))
        .AddFile(Path.Join(Root, "file.txt"), FileBody, FileTime)
        .AddFile(Path.Join(Root, "big.txt"), RecordedFixture.ReadBytes("blksize-1024-read", "stdout.bin"), FileTime)
        .AddFile(Path.Join(Root, "exact-512.txt"), RecordedFixture.ReadBytes("exactly-512-bytes", "stdout.bin"), FileTime);

    private static ExchangeContext Context(IExchangeLog log, TimeProvider clock, CancellationToken cancellationToken = default) => new(
        1,
        new ListenUrl("tftp", "127.0.0.1", ScriptedDatagramFlow.ListenPort).WithBoundPort(ScriptedDatagramFlow.ListenPort),
        new IPEndPoint(IPAddress.Loopback, ScriptedDatagramFlow.ListenPort),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        clock,
        cancellationToken);

    private static async Task<(ScriptedDatagramFlow Flow, RecordingExchangeLog Log)> ServeAsync(
        byte[] firstDatagram,
        IEnumerable<ScriptedDatagramFlow.Step> script,
        IContentFileSystem? fileSystem = null)
    {
        var clock = new ManualTimeProvider();
        var flow = new ScriptedDatagramFlow(firstDatagram, clock, script);
        var log = new RecordingExchangeLog();
        var server = new TftpProtocolServer(new ContentStore(Root, fileSystem ?? StandardFileSystem(), ContentExposureOptions.ServeEverythingInsideTheRoot));

        await server.ServeAsync(flow, Context(log, clock));

        return (flow, log);
    }

    /// <summary>
    /// A flow that cancels the exchange the first time the server waits for a datagram.
    /// </summary>
    private sealed class CancellingDatagramFlow(ScriptedDatagramFlow inner, CancellationTokenSource exchange) : IDatagramFlow
    {
        public EndPoint LocalEndPoint => inner.LocalEndPoint;

        public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

        public ReadOnlyMemory<byte> FirstDatagram => inner.FirstDatagram;

        public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
        {
            exchange.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return inner.ReceiveAsync(cancellationToken);
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken) => inner.SendAsync(datagram, cancellationToken);

        public ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken) => inner.MoveToNewLocalPortAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
