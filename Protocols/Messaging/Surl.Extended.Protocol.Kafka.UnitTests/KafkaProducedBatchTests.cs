namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>A batch's producer fields (BL-951; ADR-0145 decision 8).</summary>
[TestClass]
public sealed class KafkaProducedBatchTests
{
    [TestMethod]
    public void LastSequence_PastTheLargestSequence_WrapsFromZero() =>
        Assert.AreEqual(1, new KafkaProducedBatch(1, 0, int.MaxValue - 1, 3).LastSequence);

    [TestMethod]
    public void RecordCountAndIsIdempotent_FollowTheFields()
    {
        var batch = new KafkaProducedBatch(0, 0, 4, 2);

        Assert.AreEqual(3, batch.RecordCount);
        Assert.AreEqual(6, batch.LastSequence);
        Assert.IsTrue(batch.IsIdempotent);
        Assert.IsFalse(new KafkaProducedBatch(-1, -1, -1, 0).IsIdempotent);
    }
}
