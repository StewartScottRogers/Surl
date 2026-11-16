using Surl.Protocol.Abstractions;

namespace Surl.Dtls;

/// <summary>
/// <see cref="DtlsServerSession"/> reporting application-data plaintext to <see cref="DtlsServerOptions.DecryptedBytes"/> (ADR-0169).
/// </summary>
[TestClass]
public sealed class DtlsServerSessionDecryptedBytesTests
{
    [TestMethod]
    public async Task ReceiveAsync_ApplicationDataRecord_ReportsItsPlaintextAsReceivedDecrypted()
    {
        var sink = new UnitTestDecryptedBytesSink();
        var (session, flow, client) = await EstablishAsync(sink);

        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.ApplicationData, [7, 8]));
        await session.ReceiveAsync(flow.Exhausted.Token);

        Assert.AreEqual("received Decrypted  0708", sink.Reports.Single());
        session.Dispose();
    }

    [TestMethod]
    public async Task SendAsync_ApplicationData_ReportsItsPlaintextAsSentDecrypted()
    {
        var sink = new UnitTestDecryptedBytesSink();
        var (session, flow, _) = await EstablishAsync(sink);

        await session.SendAsync(new byte[] { 1, 2, 3 }, flow.Exhausted.Token);

        Assert.AreEqual("sent Decrypted  010203", sink.Reports.Single());
        session.Dispose();
    }

    [TestMethod]
    public async Task HandshakeAndAlertRecords_ReportNothing()
    {
        var sink = new UnitTestDecryptedBytesSink();
        var (session, flow, client) = await EstablishAsync(sink);

        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.Alert, [1, (byte)DtlsAlertDescription.NoRenegotiation]));
        flow.DeliverFromClient(client.ProtectedRecord(DtlsContentType.Alert, [1, 0]));
        await session.ReceiveAsync(flow.Exhausted.Token);

        Assert.IsEmpty(sink.Reports);
        session.Dispose();
    }

    [TestMethod]
    public async Task SendAsync_WithDiscardingAndRecordingSinks_SendsRecordsOfTheSameShape()
    {
        var (discarding, discardingFlow, _) = await EstablishAsync(DiscardingDecryptedBytesSink.Instance);
        var (recording, recordingFlow, _) = await EstablishAsync(new UnitTestDecryptedBytesSink());

        await discarding.SendAsync(new byte[] { 9, 9, 9 }, discardingFlow.Exhausted.Token);
        await recording.SendAsync(new byte[] { 9, 9, 9 }, recordingFlow.Exhausted.Token);

        var first = UnitTestRecords.Split(discardingFlow.Sent[^1]).Single();
        var second = UnitTestRecords.Split(recordingFlow.Sent[^1]).Single();
        Assert.AreEqual(first.Header, second.Header);
        Assert.AreEqual(discardingFlow.Sent[^1].Length, recordingFlow.Sent[^1].Length);
        discarding.Dispose();
        recording.Dispose();
    }

    private static async Task<(DtlsServerSession Session, UnitTestDatagramFlow Flow, UnitTestDtlsClient Client)> EstablishAsync(IDecryptedBytesSink sink)
    {
        var client = new UnitTestDtlsClient();
        var (session, flow) = DtlsServerSessionTests.Start(client, configure: options => options with { DecryptedBytes = sink });
        var outcome = await session.HandshakeAsync(flow.Exhausted.Token);
        Assert.AreEqual(DtlsHandshakeResult.Completed, outcome.Result);
        return (session, flow, client);
    }

    private sealed class UnitTestDecryptedBytesSink : IDecryptedBytesSink
    {
        public List<string> Reports { get; } = [];

        public void Received(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes) =>
            Reports.Add($"received {layer} {streamId} {Convert.ToHexString(bytes)}");

        public void Sent(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes) =>
            Reports.Add($"sent {layer} {streamId} {Convert.ToHexString(bytes)}");
    }
}
