using System.Text;
using static Surl.MailStore.PersistedStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// Pins the mail store index's bytes, written by hand from ADR-0050 decision 7's table.
/// </summary>
[TestClass]
public sealed class MailStoreIndexTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Encode_EmptyStore_IsTheHeaderTheTwoNumbersAndNoOwners()
    {
        var bytes = MailStoreIndex.Encode(0, 0, []);

        Assert.AreEqual(
            "5355524C2D4D41494C2D494E4445582D310A" // SURL-MAIL-INDEX-1 LF
            + "0000000000000000" // next message file number
            + "00000000" // last UIDVALIDITY given
            + "00000000", // owner count
            Convert.ToHexString(bytes));
    }

    [TestMethod]
    public async Task SaveChangesAsync_TwoMailboxesAndThreeMessages_WritesTheIndexTheAdrLaysOut()
    {
        var fileSystem = NewFileSystem();
        var store = await LoadAsync(fileSystem, ["al"]);
        var view = store.ViewFor("al");
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(view, "Sent"));
        Assert.AreEqual(MailStoreOutcome.Succeeded, MailStoreFixture.Deliver(store, "one\r\n", "al@x"));
        var appendedDate = new DateTimeOffset(2026, 9, 28, 10, 30, 0, TimeSpan.FromMinutes(330));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(view, "Sent", "two!"u8, MailFlags.Seen | MailFlags.Draft, appendedDate, out _));
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.Copy(view, "INBOX", [1], "Sent", out _));

        await store.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual(
            "5355524C2D4D41494C2D494E4445582D310A" // SURL-MAIL-INDEX-1 LF
            + "0000000000000002" // next message file number
            + "6ABB7001" // last UIDVALIDITY given: 2026-09-29T08:00:00Z plus one
            + "00000001" // owner count
            + "0002" + "616C" + "00000002" // owner "al", two mailboxes
            + "0005" + "494E424F58" + "6ABB7000" + "00000002" + "00000001" // INBOX, UIDVALIDITY, next UID 2, one message
            + "00000001" + "00" + "000000006ABB7000" + "0000" + "0000000000000005" + "0000000000000000" // UID 1, no flags, delivered at 08:00Z, 5 bytes, file 0
            + "0004" + "53656E74" + "6ABB7001" + "00000003" + "00000002" // Sent, UIDVALIDITY, next UID 3, two messages
            + "00000001" + "11" + "000000006AB9F450" + "014A" + "0000000000000004" + "0000000000000001" // UID 1, \Seen \Draft, 10:30+05:30, 4 bytes, file 1
            + "00000002" + "00" + "000000006ABB7000" + "0000" + "0000000000000005" + "0000000000000000", // UID 2, the copy: file 0 shared
            Convert.ToHexString(ReadFile(fileSystem, IndexPath)));
        Assert.AreEqual("one\r\n", Encoding.ASCII.GetString(ReadFile(fileSystem, MessagePath(0))));
        Assert.AreEqual("two!", Encoding.ASCII.GetString(ReadFile(fileSystem, MessagePath(1))));
    }

    [TestMethod]
    public async Task SaveChangesAsync_OwnerWithNoMailboxes_IsNotWritten()
    {
        var fileSystem = NewFileSystem();
        WriteFile(fileSystem, IndexPath, UnitTestIndexBytes.Index(0, 5, UnitTestIndexBytes.Owner("gone")));
        var store = await LoadAsync(fileSystem, ["al"]);

        await store.SaveChangesAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            UnitTestIndexBytes.Index(0, 0x6ABB7000, UnitTestIndexBytes.Owner("al", UnitTestIndexBytes.Mailbox("INBOX", 0x6ABB7000, 1))),
            ReadFile(fileSystem, IndexPath));
    }
}
