using System.Text;
using Surl.Authentication;
using Surl.Content;
using Surl.Core;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// An account added or removed while the server runs gets or loses its mail-store owner (ADR-0185, BL-1452).
/// </summary>
[TestClass]
public sealed class ComposedStoresMailOwnerTests
{
    private const string Message = "Subject: one\r\n\r\nbody\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AddAccount_AMailAccount_CanBeSeededAndHasAnInbox()
    {
        await using var serving = await ComposeAsync("-u", "alice:secret");
        var stores = serving.Stores;

        stores.AddAccount(new Account("task-1", "pw"));
        var seeded = await stores.SeedMailAsync("task-1@example.com", Encoding.ASCII.GetBytes(Message), TestContext.CancellationToken);

        Assert.AreEqual(MailRecipientLookup.Deliverable, seeded.Recipient);
        Assert.AreEqual("INBOX", stores.ListMailboxes("task-1").Value!.Single().Name);
    }

    [TestMethod]
    public async Task SeedMailAsync_PublishesDeliveredForHoldingTheRecipientAddress()
    {
        await using var serving = await ComposeAsync("-u", "alice:secret");
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        await serving.Stores.SeedMailAsync("<alice@example.com>", Encoding.ASCII.GetBytes(Message), TestContext.CancellationToken);

        var seeded = new List<StoreChangedEvent>();
        while (subscription.TryRead(out var next))
        {
            if (next is StoreChangedEvent { Store: StoreKind.Mail, Change: StoreChange.Seeded } change)
            {
                seeded.Add(change);
            }
        }

        CollectionAssert.AreEqual(new[] { "alice@example.com" }, seeded.Single().DeliveredFor!.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MailDeliveredToEnabled_FollowsTheMailDeliveredToOption(bool on)
    {
        await using var serving = await ComposeAsync(on ? ["-u", "alice:secret", "--mail-delivered-to"] : ["-u", "alice:secret"]);

        Assert.AreEqual(on, serving.Stores.MailDeliveredToEnabled);
    }

    [TestMethod]
    public async Task AddAccount_AnAccountRestrictedToANonMailScheme_GetsNoOwner()
    {
        await using var serving = await ComposeAsync("-u", "alice:secret");
        var stores = serving.Stores;

        stores.AddAccount(new Account("web", "pw") { Schemes = new HashSet<string> { "http" } });

        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.ListMailboxes("web").Outcome);
    }

    [TestMethod]
    public async Task RemoveAccount_AStartTimeAccount_RetiresItsOwner()
    {
        await using var serving = await ComposeAsync("-u", "alice:secret");
        var stores = serving.Stores;

        Assert.IsTrue(stores.RemoveAccount("alice"));
        Assert.IsFalse(stores.RemoveAccount("alice"));
        var seeded = await stores.SeedMailAsync("alice@example.com", Encoding.ASCII.GetBytes(Message), TestContext.CancellationToken);

        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, seeded.Recipient);
        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.ListMailboxes("alice").Outcome);
    }

    [TestMethod]
    public async Task SetAccountPasswordAsync_ForAnExistingName_KeepsTheOwnersMessages()
    {
        await using var serving = await ComposeAsync("-u", "alice:secret");
        var stores = serving.Stores;
        await stores.SeedMailAsync("alice@example.com", Encoding.ASCII.GetBytes(Message), TestContext.CancellationToken);
        var target = new ComposedControlTarget(serving.Events, serving.Faults, stores);

        await target.SetAccountPasswordAsync("alice", "changed", TestContext.CancellationToken);

        Assert.AreEqual(1, stores.ListMailboxes("alice").Value!.Single().MessageCount);
    }

    [TestMethod]
    public async Task ResetAsync_AfterAnAddAndARemove_LeavesMailOwnersAndAccountsConsistent()
    {
        await using var serving = await ComposeAsync("-u", "alice:secret");
        var stores = serving.Stores;
        stores.AddAccount(new Account("task-1", "pw"));
        stores.RemoveAccount("alice");

        await stores.ResetAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "alice" }, stores.Accounts.UserNames.ToArray());
        Assert.AreEqual("INBOX", stores.ListMailboxes("alice").Value!.Single().Name);
        Assert.AreEqual(ComposedReadOutcome.NotFound, stores.ListMailboxes("task-1").Outcome);
        Assert.AreEqual(MailRecipientLookup.NoSuchAccount,
            (await stores.SeedMailAsync("task-1@example.com", Encoding.ASCII.GetBytes(Message), TestContext.CancellationToken)).Recipient);
    }

    [TestMethod]
    public async Task ListMailboxes_AConfiguredUse_IsListedAndSurvivesAResetButACreatedUseDoesNot()
    {
        await using var serving = await ComposeAsync("--mail-domain", "example.com", "--mailbox", "ann@example.com:s;folders=Sent=sent,Work", "--user", "bob:pw");
        var stores = serving.Stores;
        var view = stores.Mail.ViewFor("ann@example.com");
        Assert.AreEqual(MailStoreOutcome.Succeeded, stores.Mail.CreateMailbox(view, "Old", null, "archive"));

        var before = stores.ListMailboxes("ann@example.com").Value!;
        Assert.AreEqual(@"\Sent", before.Single(box => box.Name == "Sent").Use);
        Assert.IsNull(before.Single(box => box.Name == "Work").Use);
        Assert.AreEqual(@"\Archive", before.Single(box => box.Name == "Old").Use);

        await stores.ResetAsync(TestContext.CancellationToken);

        var after = stores.ListMailboxes("ann@example.com").Value!;
        Assert.AreEqual(@"\Sent", after.Single(box => box.Name == "Sent").Use);
        Assert.IsNull(after.Single(box => box.Name == "INBOX").Use);
        Assert.IsFalse(after.Any(box => box.Name == "Old"));
    }

    private async Task<ComposedServing> ComposeAsync(params string[] options)
    {
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse([.. options, "http://127.0.0.1:0/"]),
            new StartTestSeams().Seams with { ContentWithoutDataDirectory = new InMemoryContentFileSystem(TimeProvider.System) },
            TestContext.CancellationToken);
        return start.Serving ?? throw new AssertFailedException(string.Join(" ", start.Failure!.Lines));
    }
}
