namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// The broker's idempotent producers (BL-951; ADR-0145 decisions 8 and 9): producer ids, the
/// sequence rules and the bound on states held.
/// </summary>
[TestClass]
public sealed class KafkaProducerStatesTests
{
    [TestMethod]
    public void Constructor_FewerThanOneState_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new KafkaProducerStates(0));

    [TestMethod]
    public void Members_NullArguments_Throw()
    {
        var states = new KafkaProducerStates();

        Assert.ThrowsExactly<ArgumentNullException>(() => states.NextProducerId(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => states.Check(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => states.Check("t", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => states.Record(null!, [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => states.Record("t", null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => states.Record("t", [], null!));
    }

    [TestMethod]
    public void NextProducerId_AtTheLargest47BitId_WrapsToZero()
    {
        var states = new KafkaProducerStates();
        var identifiers = new FixedBytesIdentifierRandom(0xFF);

        long[] ids = [states.NextProducerId(identifiers), states.NextProducerId(identifiers)];

        CollectionAssert.AreEqual(new[] { (1L << 47) - 1, 0L }, ids);
    }

    [TestMethod]
    public void Record_The10001stProducerState_DropsTheLeastRecentlyUsedWhoseNextBatchIsThenAcceptedAtAnySequence()
    {
        var states = new KafkaProducerStates();
        for (var producerId = 0L; producerId < KafkaProducerStates.DefaultMaxProducerStates; producerId++)
        {
            states.Record("t", [Batch(producerId, 0)], [producerId]);
        }

        states.Record("t", [Batch(0, 1)], [10000]);
        states.Record("t", [Batch(10000, 0)], [10001]);

        Assert.AreEqual(KafkaProducerStates.DefaultMaxProducerStates, states.Count);
        Assert.AreEqual(KafkaSequenceCheck.Append, states.Check("t", [Batch(1, 99)]));
        Assert.AreEqual(KafkaErrorCode.OutOfOrderSequenceNumber, states.Check("t", [Batch(0, 99)]).Error);
        Assert.AreEqual(KafkaErrorCode.OutOfOrderSequenceNumber, states.Check("t", [Batch(2, 99)]).Error);
    }

    [TestMethod]
    public void Check_ARepeatOfTheSixthBatchBack_IsOutOfOrderAndOfTheFifthIsADuplicate()
    {
        var states = new KafkaProducerStates();
        for (var sequence = 0; sequence < 6; sequence++)
        {
            states.Record("t", [Batch(1, sequence)], [sequence * 10L]);
        }

        Assert.AreEqual(KafkaErrorCode.OutOfOrderSequenceNumber, states.Check("t", [Batch(1, 0)]).Error);
        Assert.AreEqual(KafkaSequenceCheck.Duplicate(10), states.Check("t", [Batch(1, 1)]));
        Assert.IsTrue(states.Check("t", [Batch(1, 1)]).IsDuplicate);
        Assert.AreEqual(KafkaSequenceCheck.Append, states.Check("t", [Batch(1, 6)]));
    }

    [TestMethod]
    public void Check_ADifferentTopic_HoldsItsOwnState()
    {
        var states = new KafkaProducerStates();
        states.Record("t", [Batch(1, 0)], [0]);

        Assert.AreEqual(KafkaSequenceCheck.Append, states.Check("u", [Batch(1, 50)]));
    }

    [TestMethod]
    public void Check_BatchesOfTwoProducersInOnePartition_AreEachJudgedAgainstTheirOwnState()
    {
        var states = new KafkaProducerStates();
        states.Record("t", [Batch(1, 0)], [0]);

        Assert.AreEqual(KafkaSequenceCheck.Append, states.Check("t", [Batch(1, 1), Batch(2, 40), Batch(-1, 0), Batch(1, 2), Batch(2, 41)]));
        Assert.AreEqual(KafkaErrorCode.OutOfOrderSequenceNumber, states.Check("t", [Batch(1, 1), Batch(1, 3)]).Error);
    }

    private static KafkaProducedBatch Batch(long producerId, int baseSequence) => new(producerId, 0, baseSequence, 0);
}
