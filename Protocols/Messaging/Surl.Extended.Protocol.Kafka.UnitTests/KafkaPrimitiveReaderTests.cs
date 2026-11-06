namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// Each primitive type of the Kafka protocol guide's "Protocol Primitive Types", written and read
/// back at its boundaries, every expected byte written by hand from the guide (BL-517). The round
/// trips cover <see cref="KafkaPrimitiveWriter"/> too; its refusals are in its own tests.
/// </summary>
[TestClass]
public sealed class KafkaPrimitiveReaderTests
{
    [TestMethod]
    [DataRow(false, new byte[] { 0x00 })]
    [DataRow(true, new byte[] { 0x01 })]
    public void Boolean_RoundTrips(bool value, byte[] expected)
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteBoolean(value);

        CollectionAssert.AreEqual(expected, writer.ToArray());
        Assert.AreEqual(value, Reader(expected).ReadBoolean());
    }

    [TestMethod]
    public void Boolean_AnyNonZeroByte_IsTrue() => Assert.IsTrue(Reader(0x7F).ReadBoolean());

    [TestMethod]
    [DataRow((sbyte)-128, (byte)0x80)]
    [DataRow((sbyte)-1, (byte)0xFF)]
    [DataRow((sbyte)0, (byte)0x00)]
    [DataRow((sbyte)127, (byte)0x7F)]
    public void Int8_RoundTripsAtItsBoundaries(sbyte value, byte expected)
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteInt8(value);

        CollectionAssert.AreEqual(new[] { expected }, writer.ToArray());
        Assert.AreEqual(value, Reader(expected).ReadInt8());
    }

    [TestMethod]
    [DataRow(short.MinValue, new byte[] { 0x80, 0x00 })]
    [DataRow((short)-1, new byte[] { 0xFF, 0xFF })]
    [DataRow((short)0x0102, new byte[] { 0x01, 0x02 })]
    [DataRow(short.MaxValue, new byte[] { 0x7F, 0xFF })]
    public void Int16_RoundTripsBigEndianAtItsBoundaries(short value, byte[] expected)
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteInt16(value);

        CollectionAssert.AreEqual(expected, writer.ToArray());
        Assert.AreEqual(value, Reader(expected).ReadInt16());
    }

    [TestMethod]
    [DataRow(int.MinValue, new byte[] { 0x80, 0x00, 0x00, 0x00 })]
    [DataRow(-1, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
    [DataRow(0x01020304, new byte[] { 0x01, 0x02, 0x03, 0x04 })]
    [DataRow(int.MaxValue, new byte[] { 0x7F, 0xFF, 0xFF, 0xFF })]
    public void Int32_RoundTripsBigEndianAtItsBoundaries(int value, byte[] expected)
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteInt32(value);

        CollectionAssert.AreEqual(expected, writer.ToArray());
        Assert.AreEqual(value, Reader(expected).ReadInt32());
    }

    [TestMethod]
    [DataRow(long.MinValue, new byte[] { 0x80, 0, 0, 0, 0, 0, 0, 0 })]
    [DataRow(-1L, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    [DataRow(0x0102030405060708L, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [DataRow(long.MaxValue, new byte[] { 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    public void Int64_RoundTripsBigEndianAtItsBoundaries(long value, byte[] expected)
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteInt64(value);

        CollectionAssert.AreEqual(expected, writer.ToArray());
        Assert.AreEqual(value, Reader(expected).ReadInt64());
    }

    [TestMethod]
    [DataRow(0u, new byte[] { 0, 0, 0, 0 })]
    [DataRow(uint.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
    public void UInt32_RoundTripsBigEndianAtItsBoundaries(uint value, byte[] expected)
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteUInt32(value);

        CollectionAssert.AreEqual(expected, writer.ToArray());
        Assert.AreEqual(value, Reader(expected).ReadUInt32());
    }

    [TestMethod]
    [DataRow(-1, new byte[] { 0x01 })]
    [DataRow(0, new byte[] { 0x00 })]
    [DataRow(1, new byte[] { 0x02 })]
    [DataRow(63, new byte[] { 0x7E })]
    [DataRow(-64, new byte[] { 0x7F })]
    [DataRow(64, new byte[] { 0x80, 0x01 })]
    [DataRow(int.MaxValue, new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0x0F })]
    [DataRow(int.MinValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F })]
    public void VarInt_RoundTripsZigzagAtItsBoundaries(int value, byte[] expected)
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteVarInt(value);

        CollectionAssert.AreEqual(expected, writer.ToArray());
        Assert.AreEqual(value, Reader(expected).ReadVarInt());
    }

    [TestMethod]
    [DataRow(-1L, new byte[] { 0x01 })]
    [DataRow(0L, new byte[] { 0x00 })]
    [DataRow(1L, new byte[] { 0x02 })]
    [DataRow(long.MaxValue, new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 })]
    [DataRow(long.MinValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 })]
    public void VarLong_RoundTripsZigzagAtItsBoundaries(long value, byte[] expected)
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteVarLong(value);

        CollectionAssert.AreEqual(expected, writer.ToArray());
        Assert.AreEqual(value, Reader(expected).ReadVarLong());
    }

    [TestMethod]
    [DataRow(0u, new byte[] { 0x00 })]
    [DataRow(127u, new byte[] { 0x7F })]
    [DataRow(128u, new byte[] { 0x80, 0x01 })]
    [DataRow(uint.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F })]
    public void UnsignedVarInt_RoundTripsAtItsBoundaries(uint value, byte[] expected)
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteUnsignedVarInt(value);

        CollectionAssert.AreEqual(expected, writer.ToArray());
        Assert.AreEqual(value, Reader(expected).ReadUnsignedVarInt());
    }

    [TestMethod]
    public void UnsignedVarInt_PastThirtyTwoBits_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0xFF, 0xFF, 0xFF, 0xFF, 0x1F).ReadUnsignedVarInt());

    [TestMethod]
    public void UnsignedVarInt_OverFiveBytes_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0x80, 0x80, 0x80, 0x80, 0x80, 0x00).ReadUnsignedVarInt());

    [TestMethod]
    public void VarLong_OverTenBytes_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(
            () => Reader(0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00).ReadVarLong());

    [TestMethod]
    public void Uuid_RoundTripsMostSignificantByteFirst()
    {
        byte[] expected = [0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];
        var value = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var writer = new KafkaPrimitiveWriter();
        writer.WriteUuid(value);

        CollectionAssert.AreEqual(expected, writer.ToArray());
        Assert.AreEqual(value, Reader(expected).ReadUuid());
    }

    [TestMethod]
    [DataRow(1.5, new byte[] { 0x3F, 0xF8, 0, 0, 0, 0, 0, 0 })]
    [DataRow(double.MaxValue, new byte[] { 0x7F, 0xEF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    [DataRow(double.NegativeInfinity, new byte[] { 0xFF, 0xF0, 0, 0, 0, 0, 0, 0 })]
    public void Float64_RoundTripsBinary64BigEndian(double value, byte[] expected)
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteFloat64(value);

        CollectionAssert.AreEqual(expected, writer.ToArray());
        Assert.AreEqual(value, Reader(expected).ReadFloat64());
    }

    [TestMethod]
    public void String_RoundTripsWithItsInt16Length()
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteString("hé");

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x03, 0x68, 0xC3, 0xA9 }, writer.ToArray());
        Assert.AreEqual("hé", Reader(writer.ToArray()).ReadString());
    }

    [TestMethod]
    public void String_Empty_IsLengthZero()
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteString(string.Empty);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00 }, writer.ToArray());
        Assert.AreEqual(string.Empty, Reader(0x00, 0x00).ReadString());
    }

    [TestMethod]
    public void String_AtTheInt16Limit_RoundTrips()
    {
        var value = new string('a', short.MaxValue);
        var writer = new KafkaPrimitiveWriter();
        writer.WriteString(value);

        Assert.AreEqual(2 + short.MaxValue, writer.Length);
        Assert.AreEqual(value, Reader(writer.ToArray()).ReadString());
    }

    [TestMethod]
    public void String_Null_IsRefusedByTheWriterAndMalformedToTheReader()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaPrimitiveWriter().WriteString(null!));
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0xFF, 0xFF).ReadString());
    }

    [TestMethod]
    public void NullableString_Null_IsLengthMinusOne()
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteNullableString(null);

        CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFF }, writer.ToArray());
        Assert.IsNull(Reader(0xFF, 0xFF).ReadNullableString());
    }

    [TestMethod]
    public void NullableString_LengthBelowMinusOne_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0xFF, 0xFE).ReadNullableString());

    [TestMethod]
    public void NullableString_LengthPastTheEnd_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0x00, 0x03, 0x61).ReadNullableString());

    [TestMethod]
    public void NullableString_IllFormedUtf8_IsMalformed()
    {
        var thrown = Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0x00, 0x01, 0xC3).ReadNullableString());

        Assert.IsInstanceOfType<System.Text.DecoderFallbackException>(thrown.InnerException);
    }

    [TestMethod]
    public void CompactString_RoundTripsWithItsLengthPlusOne()
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteCompactString("ab");

        CollectionAssert.AreEqual(new byte[] { 0x03, 0x61, 0x62 }, writer.ToArray());
        Assert.AreEqual("ab", Reader(0x03, 0x61, 0x62).ReadCompactString());
    }

    [TestMethod]
    public void CompactString_Empty_IsLengthOne() =>
        Assert.AreEqual(string.Empty, Reader(0x01).ReadCompactString());

    [TestMethod]
    public void CompactString_Null_IsRefusedByTheWriterAndMalformedToTheReader()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new KafkaPrimitiveWriter().WriteCompactString(null!));
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0x00).ReadCompactString());
    }

    [TestMethod]
    public void CompactNullableString_Null_IsZero()
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteCompactNullableString(null);

        CollectionAssert.AreEqual(new byte[] { 0x00 }, writer.ToArray());
        Assert.IsNull(Reader(0x00).ReadCompactNullableString());
    }

    [TestMethod]
    public void CompactNullableString_LengthPastInt32_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0x80, 0x80, 0x80, 0x80, 0x08).ReadCompactNullableString());

    [TestMethod]
    public void Bytes_RoundTripWithTheirInt32Length()
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteBytes([0xAB, 0xCD]);

        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 2, 0xAB, 0xCD }, writer.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCD }, Reader(0, 0, 0, 2, 0xAB, 0xCD).ReadBytes().ToArray());
    }

    [TestMethod]
    public void Bytes_Null_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0xFF, 0xFF, 0xFF, 0xFF).ReadBytes());

    [TestMethod]
    public void NullableBytes_NullAndEmpty_AreMinusOneAndZero()
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteNullableBytes(null);
        writer.WriteNullableBytes(ReadOnlyMemory<byte>.Empty);
        var reader = Reader(writer.ToArray());

        CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0 }, writer.ToArray());
        Assert.IsNull(reader.ReadNullableBytes());
        Assert.AreEqual(0, reader.ReadNullableBytes()!.Value.Length);
    }

    [TestMethod]
    public void NullableBytes_LengthBelowMinusOne_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0xFF, 0xFF, 0xFF, 0xFE).ReadNullableBytes());

    [TestMethod]
    public void ReadRawBytes_ACount_ReadsThatManyBytesWithNoPrefix()
    {
        var reader = Reader(1, 2, 3);

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, reader.ReadRawBytes(2).ToArray());
        Assert.AreEqual(1, reader.Remaining);
    }

    [TestMethod]
    public void ReadRawBytes_MoreThanAreLeft_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(1).ReadRawBytes(2));

    [TestMethod]
    public void ReadRawBytes_ANegativeCount_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Reader(1).ReadRawBytes(-1));

    [TestMethod]
    public void CompactBytes_RoundTripWithTheirLengthPlusOne()
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteCompactBytes([0x01]);

        CollectionAssert.AreEqual(new byte[] { 0x02, 0x01 }, writer.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x01 }, Reader(0x02, 0x01).ReadCompactBytes().ToArray());
    }

    [TestMethod]
    public void CompactBytes_Null_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0x00).ReadCompactBytes());

    [TestMethod]
    public void CompactNullableBytes_NullAndValue_RoundTrip()
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteCompactNullableBytes(null);
        writer.WriteCompactNullableBytes(new byte[] { 0x07 });
        var reader = Reader(writer.ToArray());

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x02, 0x07 }, writer.ToArray());
        Assert.IsNull(reader.ReadCompactNullableBytes());
        CollectionAssert.AreEqual(new byte[] { 0x07 }, reader.ReadCompactNullableBytes()!.Value.ToArray());
    }

    [TestMethod]
    public void ArrayLength_NullAndCount_RoundTrip()
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteArrayLength(null);
        writer.WriteArrayLength(2);
        writer.WriteRaw([0x0A, 0x0B]);
        var reader = Reader(writer.ToArray());

        CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 2, 0x0A, 0x0B }, writer.ToArray());
        Assert.IsNull(reader.ReadArrayLength());
        Assert.AreEqual(2, reader.ReadArrayLength());
    }

    [TestMethod]
    public void ArrayLength_MoreElementsThanBytesLeft_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0, 0, 0, 2, 0x0A).ReadArrayLength());

    [TestMethod]
    public void ArrayLength_BelowMinusOne_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0xFF, 0xFF, 0xFF, 0xFE).ReadArrayLength());

    [TestMethod]
    public void CompactArrayLength_NullAndCount_RoundTrip()
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteCompactArrayLength(null);
        writer.WriteCompactArrayLength(1);
        writer.WriteRaw([0x0A]);
        var reader = Reader(writer.ToArray());

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x02, 0x0A }, writer.ToArray());
        Assert.IsNull(reader.ReadCompactArrayLength());
        Assert.AreEqual(1, reader.ReadCompactArrayLength());
    }

    [TestMethod]
    public void TaggedFields_Empty_IsOneZeroByte()
    {
        var writer = new KafkaPrimitiveWriter();
        writer.WriteEmptyTaggedFields();

        CollectionAssert.AreEqual(new byte[] { 0x00 }, writer.ToArray());
        Assert.AreEqual(0, Reader(0x00).SkipTaggedFields());
    }

    [TestMethod]
    public void TaggedFields_EachFieldsTagSizeAndBytes_AreSkipped()
    {
        // Two fields: tag 0 with 2 bytes, tag 1 with none; then the INT8 that follows them.
        var reader = Reader(0x02, 0x00, 0x02, 0xAA, 0xBB, 0x01, 0x00, 0x2A);

        Assert.AreEqual(2, reader.SkipTaggedFields());
        Assert.AreEqual((sbyte)42, reader.ReadInt8());
        Assert.AreEqual(0, reader.Remaining);
    }

    [TestMethod]
    public void TaggedFields_SizePastTheEnd_IsMalformed() =>
        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => Reader(0x01, 0x00, 0x05, 0xAA).SkipTaggedFields());

    [TestMethod]
    public void Reader_FixedWidthPastTheEnd_IsMalformedAndPositionStays()
    {
        var reader = Reader(0x01, 0x02, 0x03);

        Assert.ThrowsExactly<KafkaMalformedRequestException>(() => reader.ReadInt32());
        Assert.AreEqual(0, reader.Position);
        Assert.AreEqual(3, reader.Remaining);
    }

    private static KafkaPrimitiveReader Reader(params byte[] bytes) => new(bytes);
}
