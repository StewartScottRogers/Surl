namespace Surl.Qpack;

[TestClass]
public sealed class QpackDynamicTableTests
{
    [TestMethod]
    public void Insert_EntryLargerThanTheCapacity_Fails()
    {
        var table = new QpackDynamicTable(64);
        table.SetCapacity(64);

        var exception = Assert.ThrowsExactly<QpackDecodingException>(
            () => table.Insert(new QpackFieldLine("a", new string('b', 32))));

        Assert.AreEqual(QpackDecodingError.EntryLargerThanCapacity, exception.Error);
        Assert.AreEqual(0, table.InsertCount);
    }

    [TestMethod]
    public void Insert_EntryOfExactlyTheCapacity_EvictsEverythingElse()
    {
        var table = new QpackDynamicTable(64);
        table.SetCapacity(64);
        table.Insert(new QpackFieldLine("a", "b"));

        table.Insert(new QpackFieldLine("a", new string('b', 31)));

        Assert.AreEqual(1, table.EvictedCount);
        Assert.AreEqual(64, table.Size);
        Assert.AreEqual(2, table.InsertCount);
    }

    [TestMethod]
    public void GetRelativeToNewest_Zero_IsTheNewestEntry()
    {
        var table = new QpackDynamicTable(100);
        table.SetCapacity(100);
        table.Insert(new QpackFieldLine("a", "1"));
        table.Insert(new QpackFieldLine("b", "2"));

        var entry = table.GetRelativeToNewest(0);

        Assert.AreEqual(new QpackFieldLine("b", "2"), entry);
    }
}
