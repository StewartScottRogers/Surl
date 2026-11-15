namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class StoreKindTests
{
    [TestMethod]
    [DataRow(StoreKind.Content, 0)]
    [DataRow(StoreKind.Mail, 1)]
    [DataRow(StoreKind.Bus, 2)]
    [DataRow(StoreKind.Accounts, 3)]
    [DataRow(StoreKind.Keyspace, 4)]
    [DataRow(StoreKind.Directory, 5)]
    [DataRow(StoreKind.Zones, 6)]
    public void Value_EachMember_HasTheNumberTheAdrAssigns(StoreKind member, int expectedNumber)
    {
        int actual = (int)member;

        Assert.AreEqual(expectedNumber, actual);
    }

    [TestMethod]
    public void Names_EveryMember_IsPinnedInOrder()
    {
        string[] names = Enum.GetNames<StoreKind>();

        CollectionAssert.AreEqual(
            new[] { "Content", "Mail", "Bus", "Accounts", "Keyspace", "Directory", "Zones" },
            names);
    }
}
