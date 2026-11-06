namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// Request headers v0, v1 and v2 (the Kafka protocol guide, "Request and Response Headers"), every
/// byte written by hand from the guide.
/// </summary>
[TestClass]
public sealed class KafkaRequestHeaderTests
{
    // api_key 3 (Metadata), api_version 4, correlation_id 7.
    private static readonly byte[] FixedFields = [0x00, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x07];

    [TestMethod]
    public void Read_VersionZero_ReadsNoClientId()
    {
        var reader = new KafkaPrimitiveReader(Following(0x2A));

        var header = KafkaRequestHeader.Read(reader, 0);

        Assert.AreEqual(new KafkaRequestHeader(3, 4, 7, null), header);
        Assert.AreEqual(KafkaRequestHeader.FixedFieldBytes, reader.Position);
    }

    [TestMethod]
    public void Read_VersionOne_ReadsTheClientId()
    {
        var reader = new KafkaPrimitiveReader(Following(0x00, 0x02, 0x63, 0x31, 0x2A));

        var header = KafkaRequestHeader.Read(reader, 1);

        Assert.AreEqual(new KafkaRequestHeader(3, 4, 7, "c1"), header);
        Assert.AreEqual((sbyte)0x2A, reader.ReadInt8());
    }

    [TestMethod]
    public void Read_VersionOneNullClientId_IsNull()
    {
        var header = KafkaRequestHeader.Read(new KafkaPrimitiveReader(Following(0xFF, 0xFF)), 1);

        Assert.IsNull(header.ClientId);
    }

    [TestMethod]
    public void Read_VersionTwo_SkipsTheTaggedFields()
    {
        // client_id stays a NULLABLE_STRING in v2; then one tagged field, tag 5, one byte.
        var reader = new KafkaPrimitiveReader(Following(0x00, 0x01, 0x63, 0x01, 0x05, 0x01, 0xEE, 0x2A));

        var header = KafkaRequestHeader.Read(reader, 2);

        Assert.AreEqual(new KafkaRequestHeader(3, 4, 7, "c"), header);
        Assert.AreEqual((sbyte)0x2A, reader.ReadInt8());
    }

    [TestMethod]
    public void Read_TooShort_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => KafkaRequestHeader.Read(new KafkaPrimitiveReader(FixedFields.AsMemory(0, 6)), 0));

    [TestMethod]
    [DataRow((short)-1)]
    [DataRow((short)3)]
    public void Read_HeaderVersionOutsideZeroToTwo_Throws(short headerVersion) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => KafkaRequestHeader.Read(new KafkaPrimitiveReader(FixedFields), headerVersion));

    [TestMethod]
    public void Read_NullReader_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaRequestHeader.Read(null!, 0));

    private static byte[] Following(params byte[] rest) => [.. FixedFields, .. rest];
}
