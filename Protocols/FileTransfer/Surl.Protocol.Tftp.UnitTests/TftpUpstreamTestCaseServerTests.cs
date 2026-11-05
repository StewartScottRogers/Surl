using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class TftpUpstreamTestCaseServerTests
{
    // Upstream test 271's <data>, and the read request its command makes curl send.
    private static readonly byte[] Chunk = Encoding.ASCII.GetBytes("a chunk of\ndata\nreturned\n to client\n");
    private static readonly byte[] AccessViolation = [0, 5, 0, 2, .. "Access violation"u8, 0];
    private static readonly byte[] FileNotFound = [0, 5, 0, 1, .. "File not found"u8, 0];

    [TestMethod]
    public void Constructor_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TftpUpstreamTestCaseServer(null!));
    }

    [TestMethod]
    public async Task ServeAsync_NullArguments_Throw()
    {
        var server = new TftpUpstreamTestCaseServer(Replies());
        var clock = new ManualTimeProvider();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(null!, Context(clock)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.ServeAsync(new ScriptedDatagramFlow(Read("/271"), clock, []), null!));
    }

    [TestMethod]
    public void Schemes_IsTftpOnly()
    {
        CollectionAssert.AreEqual(new[] { "tftp" }, new TftpUpstreamTestCaseServer(Replies()).Schemes.ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_Test271Read_SendsTheCaseDataAndLogsTheRequestAsUpstreamsServerDoes()
    {
        var replies = Replies();
        var request = TftpProtocolServerTests.ReadRequest("/271", "tsize", "0", "blksize", "512", "timeout", "6");

        var flow = await ServeAsync(replies, request, Ack(0), Ack(1));

        CollectionAssert.AreEqual(Chunk, DataPayloads(flow));
        Assert.AreEqual("opcode = 1\nmode = octet\ntsize = 0\nblksize = 512\ntimeout = 6\nfilename = /271\n", Encoding.ASCII.GetString(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task ServeAsync_ReadOfANumberOver10000_SendsDataNum()
    {
        var replies = Replies(("data3", "third part\n"u8.ToArray()));

        var flow = await ServeAsync(replies, Read("/20020003"), Ack(1));

        CollectionAssert.AreEqual("third part\n"u8.ToArray(), DataPayloads(flow));
    }

    [TestMethod]
    [DataRow("/invalid-file")]
    [DataRow("/272")]
    [DataRow("../271")]
    public async Task ServeAsync_ReadOfANameTheCaseHasNoFileFor_AnswersError1(string fileName)
    {
        var replies = Replies();

        var flow = await ServeAsync(replies, Read(fileName.Replace("272", "20020005", StringComparison.Ordinal)));

        CollectionAssert.AreEqual(FileNotFound, flow.Sent.Single().Bytes);
        StringAssert.EndsWith(Encoding.ASCII.GetString(replies.ReceivedBytes), "filename = " + fileName.Replace("272", "20020005", StringComparison.Ordinal) + "\n");
    }

    [TestMethod]
    public async Task ServeAsync_WriteToACaseFile_KeepsTheUpload()
    {
        var replies = Replies();

        var flow = await ServeAsync(replies, Write("/test285.txt"), [0, 3, 0, 1, .. Chunk]);

        CollectionAssert.AreEqual(new byte[] { 0, 4, 0, 1 }, flow.Sent[^1].Bytes);
        CollectionAssert.AreEqual(Chunk, replies.UploadedBytes);
        Assert.AreEqual("opcode = 2\nmode = octet\nfilename = /test285.txt\n", Encoding.ASCII.GetString(replies.ReceivedBytes));
    }

    [TestMethod]
    public async Task ServeAsync_WriteToANameWithoutACaseNumber_AnswersError2AndKeepsNothing()
    {
        var replies = Replies();

        var flow = await ServeAsync(replies, Write("/invalid-file"));

        CollectionAssert.AreEqual(AccessViolation, flow.Sent.Single().Bytes);
        Assert.IsEmpty(replies.UploadedBytes);
    }

    [TestMethod]
    public async Task ServeAsync_AFirstDatagramThatIsNoRequest_IsAnsweredAsSurlsServerAnswersItAndNotLogged()
    {
        var replies = Replies();

        var flow = await ServeAsync(replies, [0, 1, (byte)'x']);

        Assert.AreEqual(TftpPacket.Error, TftpPacket.ReadOpcode(flow.Sent.Single().Bytes));
        Assert.IsEmpty(replies.ReceivedBytes);
    }

    [TestMethod]
    public async Task ServeAsync_WriteDelay_WaitsBeforeTheDataPacket()
    {
        var replies = Replies(serverCommands: ["writedelay: 2000"]);
        var clock = new ManualTimeProvider();
        var flow = new ScriptedDatagramFlow(Read("/271"), clock, [ScriptedDatagramFlow.Datagram(Ack(1))]);

        var serving = new TftpUpstreamTestCaseServer(replies).ServeAsync(flow, Context(clock));
        var sentBeforeTheDelay = flow.Sent.Count;
        clock.Advance(TimeSpan.FromSeconds(2));
        await serving;

        Assert.AreEqual(0, sentBeforeTheDelay);
        CollectionAssert.AreEqual(Chunk, DataPayloads(flow));
    }

    private static TftpUpstreamTestCaseReplies Replies(params (string Name, byte[] Bytes)[] parts) => Replies(parts, []);

    private static TftpUpstreamTestCaseReplies Replies((string Name, byte[] Bytes)[]? parts = null, IEnumerable<string>? serverCommands = null)
    {
        var all = new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["data"] = Chunk };
        foreach (var (name, bytes) in parts ?? [])
        {
            all[name] = bytes;
        }

        return new TftpUpstreamTestCaseReplies(all, serverCommands ?? []);
    }

    private static async Task<ScriptedDatagramFlow> ServeAsync(TftpUpstreamTestCaseReplies replies, byte[] request, params byte[][] client)
    {
        var clock = new ManualTimeProvider();
        var flow = new ScriptedDatagramFlow(request, clock, client.Select(ScriptedDatagramFlow.Datagram));

        await new TftpUpstreamTestCaseServer(replies).ServeAsync(flow, Context(clock));

        return flow;
    }

    private static byte[] Read(string fileName) => TftpProtocolServerTests.ReadRequest(fileName);

    private static byte[] Write(string fileName) => [0, 2, .. Encoding.ASCII.GetBytes(fileName + "\0octet\0")];

    private static byte[] Ack(ushort blockNumber) => [0, 4, (byte)(blockNumber >> 8), (byte)blockNumber];

    private static byte[] DataPayloads(ScriptedDatagramFlow flow) =>
        [.. flow.SentBytes.Where(sent => sent[1] == TftpPacket.Data).SelectMany(sent => sent.Skip(4))];

    private static ExchangeContext Context(TimeProvider clock) => new(
        1,
        new ListenUrl("tftp", "127.0.0.1", ScriptedDatagramFlow.ListenPort).WithBoundPort(ScriptedDatagramFlow.ListenPort),
        new IPEndPoint(IPAddress.Loopback, ScriptedDatagramFlow.ListenPort),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        clock,
        CancellationToken.None);
}
