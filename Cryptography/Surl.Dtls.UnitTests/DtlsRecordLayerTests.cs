using Surl.Protocol.Abstractions;

namespace Surl.Dtls;

[TestClass]
public sealed class DtlsRecordLayerTests
{
    private static readonly byte[] Key = new byte[16];
    private static readonly byte[] Salt = [9, 8, 7, 6];

    [TestMethod]
    public void ReadFirstDatagram_PlaintextHandshakeRecord_ReadsIt()
    {
        var first = new DtlsRecordWriter(epoch: 0, protection: null).Seal(DtlsContentType.Handshake, [0x01]);
        using var layer = new DtlsRecordLayer(new InMemoryDatagramFlow(first));

        var read = layer.ReadFirstDatagram();

        Assert.HasCount(1, read.Records);
        Assert.AreEqual((ushort)0, layer.ReadEpoch);
    }

    [TestMethod]
    public async Task ReceiveAsync_ProtectedRecordAfterTheReadEpochStarts_ReadsItAndDropsEpochZero()
    {
        var clientWriter = new DtlsRecordWriter(epoch: 1, new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt));
        var stale = new DtlsRecordWriter(epoch: 0, protection: null).Seal(DtlsContentType.Handshake, [0x01]);
        var protectedRecord = clientWriter.Seal(DtlsContentType.ApplicationData, [0x2A]);
        var flow = new InMemoryDatagramFlow(stale, [stale, protectedRecord]);
        using var layer = new DtlsRecordLayer(flow);
        layer.StartProtectedReadEpoch(new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt));

        var staleRead = await layer.ReceiveAsync(TestContext.CancellationToken);
        var protectedRead = await layer.ReceiveAsync(TestContext.CancellationToken);

        Assert.AreEqual((ushort)1, layer.ReadEpoch);
        CollectionAssert.AreEqual(new[] { DtlsRecordDropReason.WrongEpoch }, staleRead.Drops.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x2A }, protectedRead.Records[0].Plaintext);
    }

    [TestMethod]
    public async Task SendRecordAsync_AfterTheWriteEpochStarts_SendsEpochOneFromSequenceNumberZero()
    {
        var flow = new InMemoryDatagramFlow(Array.Empty<byte>());
        using var layer = new DtlsRecordLayer(flow);
        await layer.SendRecordAsync(DtlsContentType.ChangeCipherSpec, new byte[] { 0x01 }, TestContext.CancellationToken);

        layer.StartProtectedWriteEpoch(new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt));
        await layer.SendRecordAsync(DtlsContentType.Handshake, new byte[] { 0x14 }, TestContext.CancellationToken);

        Assert.HasCount(2, flow.SentDatagrams);
        DtlsRecordHeader.TryRead(flow.SentDatagrams[1], out var header);
        Assert.AreEqual((ushort)1, header.Epoch);
        Assert.AreEqual(0, header.SequenceNumber);
        Assert.AreEqual((ushort)1, layer.WriteEpoch);
        Assert.AreEqual(1, layer.NextWriteSequenceNumber);
        var reader = new DtlsRecordReader(epoch: 1, new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt));
        CollectionAssert.AreEqual(new byte[] { 0x14 }, reader.ReadDatagram(flow.SentDatagrams[1]).Records[0].Plaintext);
    }

    [TestMethod]
    public void SealRecord_PlaintextEpoch_ReturnsTheRecordWithoutSendingIt()
    {
        var flow = new InMemoryDatagramFlow(Array.Empty<byte>());
        using var layer = new DtlsRecordLayer(flow);

        var record = layer.SealRecord(DtlsContentType.Handshake, [0x01]);

        Assert.HasCount(DtlsRecordHeader.Size + 1, record);
        Assert.IsEmpty(flow.SentDatagrams);
        Assert.AreEqual(1, layer.NextWriteSequenceNumber);
    }

    [TestMethod]
    public void StartProtectedReadEpoch_Twice_IsRefused()
    {
        using var layer = new DtlsRecordLayer(new InMemoryDatagramFlow(Array.Empty<byte>()));
        layer.StartProtectedReadEpoch(new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt));
        using var second = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);

        Assert.ThrowsExactly<InvalidOperationException>(() => layer.StartProtectedReadEpoch(second));
    }

    [TestMethod]
    public void StartProtectedWriteEpoch_Twice_IsRefused()
    {
        using var layer = new DtlsRecordLayer(new InMemoryDatagramFlow(Array.Empty<byte>()));
        layer.StartProtectedWriteEpoch(new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt));
        using var second = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);

        Assert.ThrowsExactly<InvalidOperationException>(() => layer.StartProtectedWriteEpoch(second));
    }

    [TestMethod]
    public void StartProtectedEpoch_NullProtection_Throws()
    {
        using var layer = new DtlsRecordLayer(new InMemoryDatagramFlow(Array.Empty<byte>()));

        Assert.ThrowsExactly<ArgumentNullException>(() => layer.StartProtectedReadEpoch(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => layer.StartProtectedWriteEpoch(null!));
    }

    [TestMethod]
    public void Dispose_ProtectedEpochs_DisposesTheirProtections()
    {
        var reading = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);
        var writing = new DtlsRecordProtection(DtlsAeadAlgorithm.AesGcm, Key, Salt);
        var layer = new DtlsRecordLayer(new InMemoryDatagramFlow(Array.Empty<byte>()));
        layer.StartProtectedReadEpoch(reading);
        layer.StartProtectedWriteEpoch(writing);

        layer.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => writing.Seal(default, [0x01]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => reading.Seal(default, [0x01]));
    }

    public TestContext TestContext { get; set; } = null!;
}
