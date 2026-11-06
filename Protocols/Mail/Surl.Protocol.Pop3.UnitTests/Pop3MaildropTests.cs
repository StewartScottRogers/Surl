using Surl.MailStore;
using static Surl.Protocol.Pop3.Pop3TestExchange;

namespace Surl.Protocol.Pop3;

[TestClass]
public sealed class Pop3MaildropTests
{
    [TestMethod]
    [DataRow("", "-ERR Invalid arguments")]
    [DataRow("x1", "-ERR Invalid arguments")]
    [DataRow("0", "-ERR Invalid arguments")]
    [DataRow("12345678901", "-ERR Invalid arguments")]
    [DataRow("3", "-ERR No such message")]
    [DataRow("9999999999", "-ERR No such message")]
    [DataRow("2", null)]
    [DataRow("0002", null)]
    public void FindMessage_Word_FindsTheMessageOrRefuses(string word, string? expected)
    {
        using var maildrop = Open(AccountStore(new ManualTimeProvider(), Message, Message));

        var refusal = maildrop.FindMessage(word, out var message);

        Assert.AreEqual(expected, refusal);
        Assert.AreEqual(refusal is null ? 2 : 0, message.Number);
    }

    [TestMethod]
    public void Delete_ThenReset_CountsAndRemovesAsMarked()
    {
        var store = AccountStore(new ManualTimeProvider(), Message, "x\r\n");
        using var maildrop = Open(store);

        Assert.IsNull(maildrop.FindMessage("1", out var first));
        maildrop.Delete(first);
        Assert.AreEqual(1, maildrop.Count);
        Assert.AreEqual(3, maildrop.Octets);
        Assert.AreEqual("-ERR No such message", maildrop.FindMessage("1", out _));
        maildrop.Reset();
        Assert.AreEqual(2, maildrop.Count);
        maildrop.Delete(first);
        CollectionAssert.AreEqual(new[] { 1 }, maildrop.Deleted.ToList());

        Assert.AreEqual(1, maildrop.RemoveDeleted());
        CollectionAssert.AreEqual(new[] { "x\r\n" }, Inbox(store, "u").ToList());
    }

    private static Pop3Maildrop Open(MailboxStore store)
    {
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.LockMaildrop(store.ViewFor("u"), out var maildropLock));
        return new Pop3Maildrop(maildropLock!, 7);
    }
}
