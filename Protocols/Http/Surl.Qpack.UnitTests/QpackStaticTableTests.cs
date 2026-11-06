namespace Surl.Qpack;

[TestClass]
public sealed class QpackStaticTableTests
{
    [TestMethod]
    [DataRow(0L, ":authority", "")]
    [DataRow(17L, ":method", "GET")]
    [DataRow(25L, ":status", "200")]
    [DataRow(58L, "strict-transport-security", "max-age=31536000; includesubdomains; preload")]
    [DataRow(85L, "content-security-policy", "script-src 'none'; object-src 'none'; base-uri 'none'")]
    [DataRow(98L, "x-frame-options", "sameorigin")]
    public void Get_Index_IsTheAppendixAEntry(long index, string name, string value)
    {
        var entry = QpackStaticTable.Get(index);

        Assert.AreEqual(new QpackFieldLine(name, value), entry);
    }

    [TestMethod]
    public void Get_Index99_IsOutsideTheTable()
    {
        var exception = Assert.ThrowsExactly<QpackDecodingException>(() => QpackStaticTable.Get(QpackStaticTable.Count));

        Assert.AreEqual(QpackDecodingError.StaticIndexOutsideTable, exception.Error);
    }

    [TestMethod]
    public void TryFindName_RepeatedName_IsItsFirstEntry()
    {
        var isFound = QpackStaticTable.TryFindName(":status", out var index);

        Assert.IsTrue(isFound);
        Assert.AreEqual(24, index);
    }

    [TestMethod]
    public void TryFindLine_LineNotInTheTable_IsNotFound()
    {
        var isFound = QpackStaticTable.TryFindLine(new QpackFieldLine(":status", "201"), out _);

        Assert.IsFalse(isFound);
    }
}
