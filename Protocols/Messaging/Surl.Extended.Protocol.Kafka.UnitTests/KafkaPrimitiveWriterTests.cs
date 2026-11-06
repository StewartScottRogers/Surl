namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// What <see cref="KafkaPrimitiveWriter"/> refuses, and its raw writes (BL-517). Its encodings are
/// pinned by the round trips in <see cref="KafkaPrimitiveReaderTests"/>.
/// </summary>
[TestClass]
public sealed class KafkaPrimitiveWriterTests
{
    [TestMethod]
    public void WriteString_OverTheInt16Limit_ThrowsArgumentException()
    {
        var writer = new KafkaPrimitiveWriter();

        Assert.ThrowsExactly<ArgumentException>(() => writer.WriteString(new string('a', short.MaxValue + 1)));
        Assert.AreEqual(0, writer.Length);
    }

    [TestMethod]
    public void WriteRaw_Bytes_AreWrittenAsTheyAre()
    {
        var writer = new KafkaPrimitiveWriter();

        writer.WriteRaw([0x01, 0x02]);

        Assert.AreEqual(2, writer.Length);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02 }, writer.ToArray());
    }
}
