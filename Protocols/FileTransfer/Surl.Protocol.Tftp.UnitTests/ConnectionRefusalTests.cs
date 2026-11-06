using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

[TestClass]
public sealed class ConnectionRefusalTests
{
    [TestMethod]
    [DataRow(ConnectionRefusal.TooManyConnections, "refused-too-many-connections")]
    [DataRow(ConnectionRefusal.TooManyConnectionsFromAddress, "refused-too-many-connections-from-address")]
    public async Task WriteRefusalAsync_SendsExactlyOneError0FromTheTransferPortAndEndsTheFlow(ConnectionRefusal refusal, string caseName)
    {
        var curl = RecordedFixture.CurlDatagrams(caseName);
        var flow = new ScriptedDatagramFlow(curl[0], new ManualTimeProvider(), [ScriptedDatagramFlow.Datagram([0, 4, 0, 0])]);
        var server = new TftpProtocolServer(UploadExchange.Store(UploadExchange.FileSystem()));

        await server.WriteRefusalAsync(flow, refusal, CancellationToken.None);

        var sent = flow.Sent.Single();
        CollectionAssert.AreEqual(RecordedFixture.ServerDatagrams(caseName).Single(), sent.Bytes);
        Assert.AreEqual(TftpPacket.Error, sent.Bytes[1]);
        Assert.AreEqual(0, sent.Bytes[3]);
        Assert.AreEqual(ScriptedDatagramFlow.TransferPort, sent.FromPort);
        Assert.AreEqual(1, flow.RemainingSteps, "The refusal reads nothing more from the flow.");
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullFlow_Throws()
    {
        var server = new TftpProtocolServer(UploadExchange.Store(UploadExchange.FileSystem()));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, CancellationToken.None));
    }
}
