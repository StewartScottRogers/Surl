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
}
