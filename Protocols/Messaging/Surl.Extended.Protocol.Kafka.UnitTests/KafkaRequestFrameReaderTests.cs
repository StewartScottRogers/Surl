using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// Size-prefixed requests read however their bytes are split (the Kafka protocol guide, "Common
/// Request and Response Structure"; ADR-0105 decision 7).
/// </summary>
[TestClass]
public sealed class KafkaRequestFrameReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadRequestAsync_RequestSplitAcrossReads_IsReassembled()
    {
        byte[] frame = [0, 0, 0, 3, 0xA1, 0xA2, 0xA3];
        var reader = Reader(frame.Select(value => (ReadOnlyMemory<byte>)new[] { value }).ToArray());

        var result = await reader.ReadRequestAsync(TestContext.CancellationToken);

        Assert.AreEqual(KafkaRequestFrameReadOutcome.RequestRead, result.Outcome);
        Assert.AreEqual(3, result.Size);
        CollectionAssert.AreEqual(new byte[] { 0xA1, 0xA2, 0xA3 }, result.Request.ToArray());
    }

    [TestMethod]
    public async Task ReadRequestAsync_TwoRequestsInOneRead_AreReadOneAtATime()
    {
        var reader = Reader(new byte[] { 0, 0, 0, 1, 0x0A, 0, 0, 0, 1, 0x0B });

        var first = await reader.ReadRequestAsync(TestContext.CancellationToken);
        var second = await reader.ReadRequestAsync(TestContext.CancellationToken);
        var third = await reader.ReadRequestAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new byte[] { 0x0A }, first.Request.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x0B }, second.Request.ToArray());
        Assert.AreEqual(KafkaRequestFrameReadOutcome.ConnectionClosed, third.Outcome);
    }

    [TestMethod]
    public async Task ReadRequestAsync_RequestOverSixtyFourKibibytes_GrowsItsBuffer()
    {
        const int size = 100000;
        byte[] prefix = [0x00, 0x01, 0x86, 0xA0];
        var body = Enumerable.Range(0, size).Select(index => (byte)index).ToArray();
        var reader = Reader(prefix, body.AsMemory(0, 70000), body.AsMemory(70000));

        var result = await reader.ReadRequestAsync(TestContext.CancellationToken);

        Assert.AreEqual(KafkaRequestFrameReadOutcome.RequestRead, result.Outcome);
        CollectionAssert.AreEqual(body, result.Request.ToArray());
    }

    [TestMethod]
    public async Task ReadRequestAsync_ClosedBeforeAnyByte_IsConnectionClosed()
    {
        var result = await Reader().ReadRequestAsync(TestContext.CancellationToken);

        Assert.AreEqual(KafkaRequestFrameReadOutcome.ConnectionClosed, result.Outcome);
    }

    [TestMethod]
    public async Task ReadRequestAsync_ClosedInsideTheSizePrefix_IsClosedMidRequest()
    {
        var result = await Reader(new byte[] { 0, 0 }).ReadRequestAsync(TestContext.CancellationToken);

        Assert.AreEqual(KafkaRequestFrameReadOutcome.ConnectionClosedMidRequest, result.Outcome);
    }

    [TestMethod]
    public async Task ReadRequestAsync_ClosedInsideTheBody_IsClosedMidRequest()
    {
        var result = await Reader(new byte[] { 0, 0, 0, 4, 0x01 }).ReadRequestAsync(TestContext.CancellationToken);

        Assert.AreEqual(KafkaRequestFrameReadOutcome.ConnectionClosedMidRequest, result.Outcome);
        Assert.AreEqual(4, result.Size);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0)]
    [DataRow(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, -1)]
    [DataRow(new byte[] { 0x00, 0x00, 0x00, 0x11 }, 17)]
    public async Task ReadRequestAsync_SizeZeroNegativeOrOverTheLimit_IsRefusedWithoutReadingTheBody(byte[] prefix, int size)
    {
        var connection = new InMemoryConnection([prefix, new byte[] { 0xEE }]);
        var reader = new KafkaRequestFrameReader(connection, 16);

        var result = await reader.ReadRequestAsync(TestContext.CancellationToken);

        Assert.AreEqual(KafkaRequestFrameReadOutcome.SizeRefused, result.Outcome);
        Assert.AreEqual(size, result.Size);
        Assert.AreEqual(1, await connection.ReadAsync(new byte[1], TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadRequestAsync_SizeAtTheLimit_IsRead()
    {
        var reader = new KafkaRequestFrameReader(new InMemoryConnection([new byte[] { 0, 0, 0, 2, 0x01, 0x02 }]), 2);

        var result = await reader.ReadRequestAsync(TestContext.CancellationToken);

        Assert.AreEqual(KafkaRequestFrameReadOutcome.RequestRead, result.Outcome);
    }

    private static KafkaRequestFrameReader Reader(params ReadOnlyMemory<byte>[] chunks) => new(new InMemoryConnection(chunks), 0);
}
