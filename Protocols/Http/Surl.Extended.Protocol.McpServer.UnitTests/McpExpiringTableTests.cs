namespace Surl.Extended.Protocol.McpServer;

[TestClass]
public sealed class McpExpiringTableTests
{
    [TestMethod]
    public void Find_EntryPastItsExpiry_IsNull()
    {
        FixedTimeProvider clock = new(McpHttpHarness.Now);
        McpExpiringTable<string> table = new(new CountingRandomNumberGenerator(), clock);
        string key = table.Add("value", McpHttpHarness.Now.AddMinutes(1))!;

        clock.UtcNow = McpHttpHarness.Now.AddMinutes(1);

        Assert.IsNull(table.Find(key));
        Assert.IsNull(table.Take(key));
    }

    [TestMethod]
    public void Take_LiveEntry_ReturnsItOnce()
    {
        McpExpiringTable<string> table = new(new CountingRandomNumberGenerator(), new FixedTimeProvider(McpHttpHarness.Now));
        string key = table.Add("value", DateTimeOffset.MaxValue)!;

        string? first = table.Take(key);
        string? second = table.Take(key);

        Assert.AreEqual("value", first);
        Assert.IsNull(second);
    }

    [TestMethod]
    public void Add_FullTable_IsNullUntilAnEntryExpires()
    {
        FixedTimeProvider clock = new(McpHttpHarness.Now);
        McpExpiringTable<string> table = new(new CountingRandomNumberGenerator(), clock);
        for (int i = 0; i < McpExpiringTable<string>.MaximumEntries; i++)
        {
            table.Add("value", McpHttpHarness.Now.AddMinutes(1));
        }

        string? whileFull = table.Add("value", DateTimeOffset.MaxValue);
        clock.UtcNow = McpHttpHarness.Now.AddMinutes(1);
        string? afterExpiry = table.Add("value", DateTimeOffset.MaxValue);

        Assert.IsNull(whileFull);
        Assert.IsNotNull(afterExpiry);
    }
}
