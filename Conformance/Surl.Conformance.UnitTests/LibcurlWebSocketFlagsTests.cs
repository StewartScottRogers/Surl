namespace Surl.Conformance;

[TestClass]
public sealed class LibcurlWebSocketFlagsTests
{
    [TestMethod]
    [DataRow("TEXT", 1u)]
    [DataRow("BINARY", 2u)]
    [DataRow("CONT", 4u)]
    [DataRow("CLOSE", 8u)]
    [DataRow("PING", 16u)]
    [DataRow("PONG", 64u)]
    public void ValueOf_FlagAScriptMayName_IsItsCurlwsValue(string name, uint value)
    {
        Assert.AreEqual(value, LibcurlWebSocketFlags.ValueOf(name));
    }

    [TestMethod]
    [DataRow("OFFSET")]
    [DataRow("text")]
    public void ValueOf_AnyOtherName_IsNull(string name)
    {
        Assert.IsNull(LibcurlWebSocketFlags.ValueOf(name));
    }

    [TestMethod]
    public void NamesOf_SeveralFlags_JoinsThemInValueOrder()
    {
        Assert.AreEqual("TEXT+CONT+OFFSET", LibcurlWebSocketFlags.NamesOf(37));
    }

    [TestMethod]
    public void NamesOf_NoFlag_IsZero()
    {
        Assert.AreEqual("0", LibcurlWebSocketFlags.NamesOf(0));
    }
}
