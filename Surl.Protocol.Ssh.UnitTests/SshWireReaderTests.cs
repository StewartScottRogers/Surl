using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshWireReaderTests
{
    [TestMethod]
    public void Read_EachDataType_ReadsItFrontToBack()
    {
        var reader = new SshWireReader(Concat([7, 0, 2], UInt32(0x01020304), String("ab"), String(string.Empty), Concat(UInt32(3), [(byte)'x', (byte)',', 0xE9])));

        Assert.AreEqual(7, reader.ReadByte());
        Assert.IsFalse(reader.ReadBoolean());
        Assert.IsTrue(reader.ReadBoolean());
        Assert.AreEqual(0x01020304u, reader.ReadUInt32());
        CollectionAssert.AreEqual(Ascii("ab"), reader.ReadString().ToArray());
        Assert.IsEmpty(reader.ReadNameList());
        CollectionAssert.AreEqual(new[] { "x", "é" }, reader.ReadNameList().ToArray());
    }

    [TestMethod]
    [DataRow(0, DisplayName = "A byte past the end")]
    [DataRow(3, DisplayName = "A uint32 cut short")]
    public void Read_PastTheEnd_IsRefusedDisconnect2(int length)
    {
        var reader = new SshWireReader(new byte[length]);

        var refusal = Assert.ThrowsExactly<SshDisconnectRequiredException>(() => length == 0 ? reader.ReadByte() : reader.ReadUInt32());

        Assert.AreEqual(SshDisconnectReason.ProtocolError, refusal.Reason);
        Assert.AreEqual("An SSH message ended inside a field.", refusal.Message);
    }

    [TestMethod]
    public void ReadString_LengthPastTheEnd_IsRefused()
    {
        var reader = new SshWireReader(Concat(UInt32(0xFFFFFFFF), [1]));

        Assert.ThrowsExactly<SshDisconnectRequiredException>(() => reader.ReadString());
    }
}
