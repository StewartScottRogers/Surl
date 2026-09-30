using System.Globalization;
using System.Numerics;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshWireWriterTests
{
    [TestMethod]
    public void ToArray_EachDataType_IsWrittenAsRfc4251Says()
    {
        var writer = new SshWireWriter();

        writer.WriteByte(7);
        writer.WriteBoolean(true);
        writer.WriteBoolean(false);
        writer.WriteUInt32(0x01020304);
        writer.WriteBytes([9, 8]);
        writer.WriteString("ab");
        writer.WriteNameList(["x", "y"]);
        writer.WriteNameList([]);

        CollectionAssert.AreEqual(
            Concat([7, 1, 0], UInt32(0x01020304), [9, 8], String("ab"), String("x,y"), String(string.Empty)),
            writer.ToArray());
    }

    // RFC 4251 section 5's mpint examples, byte for byte.
    public static IEnumerable<object[]> Rfc4251MpintExamples() =>
    [
        [BigInteger.Zero, new byte[] { 0x00, 0x00, 0x00, 0x00 }],
        [BigInteger.Parse("09A378F9B2E332A7", NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            new byte[] { 0x00, 0x00, 0x00, 0x08, 0x09, 0xA3, 0x78, 0xF9, 0xB2, 0xE3, 0x32, 0xA7 }],
        [new BigInteger(0x80), new byte[] { 0x00, 0x00, 0x00, 0x02, 0x00, 0x80 }],
        [new BigInteger(-0x1234), new byte[] { 0x00, 0x00, 0x00, 0x02, 0xED, 0xCC }],
        [new BigInteger(-0xDEADBEEFL), new byte[] { 0x00, 0x00, 0x00, 0x05, 0xFF, 0x21, 0x52, 0x41, 0x11 }],
    ];

    [TestMethod]
    [DynamicData(nameof(Rfc4251MpintExamples))]
    public void WriteMpint_Rfc4251Examples_AreWrittenAsTheRfcShowsThem(BigInteger value, byte[] expected)
    {
        var writer = new SshWireWriter();

        writer.WriteMpint(value);

        CollectionAssert.AreEqual(expected, writer.ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(Rfc4251MpintExamples))]
    public void ReadMpint_Rfc4251Examples_AreReadBack(BigInteger expected, byte[] bytes)
    {
        Assert.AreEqual(expected, new SshWireReader(bytes).ReadMpint());
    }

    [TestMethod]
    public void WriteString_Bytes_IsLengthThenBytes()
    {
        var writer = new SshWireWriter();

        writer.WriteString(new byte[] { 0xFF, 0x00 });

        CollectionAssert.AreEqual(Concat(UInt32(2), [0xFF, 0x00]), writer.ToArray());
    }
}
