namespace Surl.Extended.Protocol.Kafka.UnitTests;

[TestClass]
public sealed class KafkaCrc32CTests
{
    [TestMethod]
    public void Compute_TheCheckString_IsTheCastagnoliCheckValue() =>
        Assert.AreEqual(0xE3069283u, KafkaCrc32C.Compute("123456789"u8));

    [TestMethod]
    public void Compute_NoBytes_IsZero() =>
        Assert.AreEqual(0u, KafkaCrc32C.Compute([]));

    [TestMethod]
    public void Compute_AnyBytes_AgreesWithTheBitByBitComputation()
    {
        var bytes = Enumerable.Range(0, 300).Select(value => (byte)(value * 7)).ToArray();

        Assert.AreEqual(KafkaRecordBatches.HandCrc32C(bytes), KafkaCrc32C.Compute(bytes));
    }
}
