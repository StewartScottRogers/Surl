using static Surl.MailStore.MailStoreFixture;
using static Surl.MailStore.PersistedStoreFixture;

namespace Surl.MailStore;

/// <summary>
/// <see cref="MailboxStore.LookUpRecipient"/> with mail domains configured (ADR-0102, decisions
/// 1.1 to 1.3), and with none (ADR-0050, decision 5).
/// </summary>
[TestClass]
public sealed class MailboxStoreMailDomainTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void LookUpRecipient_MailboxInAConfiguredDomain_IsItsOwnerWhateverTheDomainsCase()
    {
        var store = DomainStore([], Configuration(mailboxes: ["Ann@example.com"]));

        CollectionAssert.AreEqual(new[] { "Ann@example.com" }, Recipient(store, "<Ann@EXAMPLE.com.>").OwnerNames.ToArray());
        Assert.AreEqual("Ann@example.com", Recipient(store, "<ann@example.com>").OwnerName);
    }

    [TestMethod]
    public void LookUpRecipient_MailboxInAnotherCase_MatchesOnlyWhenOneMailboxEqualsIgnoringCase()
    {
        var store = DomainStore([], Configuration(mailboxes: ["Ann@example.com", "ANN@example.com"]));

        Assert.AreEqual("Ann@example.com", Recipient(store, "<Ann@example.com>").OwnerName);
        Assert.AreEqual("ANN@example.com", Recipient(store, "<ANN@example.com>").OwnerName);
        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<ann@example.com>", out var recipient));
        Assert.IsNull(recipient);
    }

    [TestMethod]
    public void LookUpRecipient_InternationalisedDomain_MatchesItsAsciiForm()
    {
        var store = DomainStore([], Configuration(domains: ["xn--bcher-kva.example"], mailboxes: ["jörg@bücher.example"]));

        Assert.AreEqual("jörg@xn--bcher-kva.example", Recipient(store, "<jörg@BÜCHER.example>").OwnerName);
    }

    [TestMethod]
    public void LookUpRecipient_AccountNamedByTheLocalPart_IsItsOwnerInEveryConfiguredDomain()
    {
        var store = DomainStore(["alice"], Configuration(domains: ["example.com", "example.org"]));

        Assert.AreEqual("alice", Recipient(store, "<alice@example.com>").OwnerName);
        Assert.AreEqual("alice", Recipient(store, "<ALICE@example.org>").OwnerName);
    }

    [TestMethod]
    public void LookUpRecipient_Alias_IsEachOfItsTargetsOwnersOnce()
    {
        var store = DomainStore(
            ["alice"],
            Configuration(
                mailboxes: ["bob@example.com"],
                aliases:
                [
                    new MailAlias("team@example.com", ["alice@example.com", "bob@example.com", "staff@example.com"]),
                    new MailAlias("staff@example.com", ["bob@example.com", "alice@example.com"]),
                ]));

        CollectionAssert.AreEqual(new[] { "alice", "bob@example.com" }, Recipient(store, "<TEAM@example.com>").OwnerNames.ToArray());
        CollectionAssert.AreEqual(new[] { "bob@example.com", "alice" }, Recipient(store, "<staff@example.com>").OwnerNames.ToArray());
    }

    [TestMethod]
    public void WithoutOwners_NoOwnerReached_IsTheSameRecipient()
    {
        var recipient = Recipient(AliasStore(), "<team@example.com>");

        Assert.AreSame(recipient, recipient.WithoutOwners(["cat@example.com"]));
    }

    [TestMethod]
    public void WithoutOwners_SomeOwnersReached_IsARecipientOfTheOthersInOrder()
    {
        var recipient = Recipient(AliasStore(), "<team@example.com>");

        var rest = recipient.WithoutOwners(["ann@example.com"]);

        Assert.IsNotNull(rest);
        CollectionAssert.AreEqual(new[] { "bob@example.com" }, rest.OwnerNames.ToArray());
    }

    [TestMethod]
    public void WithoutOwners_EveryOwnerReached_IsNull()
    {
        var recipient = Recipient(AliasStore(), "<team@example.com>");

        Assert.IsNull(recipient.WithoutOwners(["bob@example.com", "ann@example.com"]));
    }

    [TestMethod]
    public void WithoutOwners_NullReachedOwners_Throws()
    {
        var recipient = Recipient(AliasStore(), "<team@example.com>");

        Assert.ThrowsExactly<ArgumentNullException>(() => recipient.WithoutOwners(null!));
    }

    private static MailboxStore AliasStore() =>
        DomainStore(
            [],
            Configuration(
                mailboxes: ["ann@example.com", "bob@example.com"],
                aliases: [new MailAlias("team@example.com", ["ann@example.com", "bob@example.com"])]));

    [TestMethod]
    public void LookUpRecipient_AliasNamingAnAccountsAddress_TakesPrecedenceOverTheAccount()
    {
        var store = DomainStore(["alice", "bob"], Configuration(aliases: [new MailAlias("alice@example.com", ["bob@example.com"])]));

        Assert.AreEqual("bob", Recipient(store, "<alice@example.com>").OwnerName);
    }

    [TestMethod]
    public void LookUpRecipient_UnknownLocalPartWithACatchAll_IsTheCatchAllsTarget()
    {
        var store = DomainStore(
            ["alice"],
            Configuration(domains: ["example.com", "example.org"], catchAlls: [new MailCatchAll("example.com", "alice@example.com")]));

        Assert.AreEqual("alice", Recipient(store, "<anyone@example.com>").OwnerName);
        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<anyone@example.org>", out var recipient));
        Assert.IsNull(recipient);
    }

    [TestMethod]
    public void LookUpRecipient_UnknownLocalPartWithNoCatchAll_IsNoSuchAccount()
    {
        var store = DomainStore(["alice"], Configuration(mailboxes: ["bob@example.com"]));

        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<carol@example.com>", out var recipient));
        Assert.IsNull(recipient);
    }

    [TestMethod]
    [DataRow("<alice@example.org>")]
    [DataRow("<alice@mail.example.com>")]
    [DataRow("<alice@[127.0.0.1]>")]
    [DataRow("<alice@a..b>")]
    [DataRow("<Postmaster>")]
    public void LookUpRecipient_OutsideEveryConfiguredDomain_IsRelayingDenied(string path)
    {
        var store = DomainStore(["alice", "postmaster"], Configuration(catchAlls: [new MailCatchAll("example.com", "alice@example.com")]));

        Assert.AreEqual(MailRecipientLookup.RelayingDenied, store.LookUpRecipient(path, out var recipient));
        Assert.IsNull(recipient);
    }

    [TestMethod]
    public void LookUpRecipient_InvalidPathWithADomainConfigured_IsInvalidAddress()
    {
        var store = DomainStore(["alice"], Configuration());

        Assert.AreEqual(MailRecipientLookup.InvalidAddress, store.LookUpRecipient("<alice>", out _));
    }

    [TestMethod]
    public void LookUpRecipient_AllowAnonymousWithADomainConfigured_IsTheAnonymousOwnerForEveryValidPath()
    {
        var store = new MailboxStore(
            ["alice"],
            allowAnonymous: true,
            new SettableTimeProvider(),
            mailDomains: Configuration(aliases: [new MailAlias("a@example.com", ["nobody@example.com"])]));

        Assert.AreEqual(string.Empty, Recipient(store, "<alice@example.com>").OwnerName);
        Assert.AreEqual(string.Empty, Recipient(store, "<alice@[127.0.0.1]>").OwnerName);
        Assert.AreEqual(string.Empty, Recipient(store, "<Postmaster>").OwnerName);
    }

    [TestMethod]
    public void LookUpRecipient_NoDomainConfigured_ResolvesByLocalPartIgnoringTheDomainAsAdr0050Says()
    {
        foreach (var mailDomains in new[] { null, new MailDomainConfiguration([], [], [], []) })
        {
            var store = new MailboxStore(["alice", "Bob", "BOB"], allowAnonymous: false, new SettableTimeProvider(), mailDomains: mailDomains);

            Assert.AreEqual("alice", Recipient(store, "<ALICE@[192.0.2.1]>").OwnerName);
            Assert.AreEqual("alice", Recipient(store, "<alice@anywhere.example>").OwnerName);
            Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<bob@example.com>", out _));
            Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<carol@example.com>", out _));
            Assert.AreEqual(MailRecipientLookup.InvalidAddress, store.LookUpRecipient("<alice>", out _));
        }
    }

    [TestMethod]
    public void Constructor_AliasThatIncludesItself_Throws()
    {
        var self = Assert.ThrowsExactly<MailRouteException>(() => DomainStore(
            ["alice"], Configuration(aliases: [new MailAlias("a@example.com", ["alice@example.com", "a@example.com"])])));
        var throughAnother = Assert.ThrowsExactly<MailRouteException>(() => DomainStore(
            [], Configuration(aliases: [new MailAlias("a@example.com", ["b@example.com"]), new MailAlias("b@example.com", ["A@example.com"])])));

        Assert.AreEqual(MailRouteKind.Alias, self.RouteKind);
        Assert.AreEqual(MailRouteProblem.IncludesItself, self.Problem);
        Assert.AreEqual("a@example.com", self.Address);
        Assert.AreEqual(MailRouteProblem.IncludesItself, throughAnother.Problem);
        Assert.AreEqual("a@example.com", throughAnother.Address);
    }

    [TestMethod]
    public void Constructor_CatchAllWhoseTargetReachesItself_Throws()
    {
        var failure = Assert.ThrowsExactly<MailRouteException>(() => DomainStore(
            [], Configuration(catchAlls: [new MailCatchAll("example.com", "nobody@example.com")])));

        Assert.AreEqual(MailRouteKind.CatchAll, failure.RouteKind);
        Assert.AreEqual(MailRouteProblem.IncludesItself, failure.Problem);
        Assert.AreEqual("nobody@example.com", failure.Address);
    }

    [TestMethod]
    [DataRow("alice@example.org")]
    [DataRow("alice@[127.0.0.1]")]
    [DataRow("alice")]
    public void Constructor_TargetOutsideEveryConfiguredDomain_Throws(string target)
    {
        var alias = Assert.ThrowsExactly<MailRouteException>(() => DomainStore(
            ["alice"], Configuration(aliases: [new MailAlias("a@example.com", [target])])));
        var catchAll = Assert.ThrowsExactly<MailRouteException>(() => DomainStore(
            ["alice"], Configuration(catchAlls: [new MailCatchAll("example.com", target)])));

        Assert.AreEqual(MailRouteKind.Alias, alias.RouteKind);
        Assert.AreEqual(MailRouteProblem.TargetOutsideMailDomains, alias.Problem);
        Assert.AreEqual(target, alias.Address);
        Assert.AreEqual(MailRouteKind.CatchAll, catchAll.RouteKind);
        Assert.AreEqual(MailRouteProblem.TargetOutsideMailDomains, catchAll.Problem);
    }

    [TestMethod]
    public void Constructor_TargetThatNamesNoMailbox_Throws()
    {
        var failure = Assert.ThrowsExactly<MailRouteException>(() => DomainStore(
            ["alice"], Configuration(aliases: [new MailAlias("a@example.com", ["alice@example.com", "carol@example.com"])])));

        Assert.AreEqual(MailRouteKind.Alias, failure.RouteKind);
        Assert.AreEqual(MailRouteProblem.TargetNamesNoMailbox, failure.Problem);
        Assert.AreEqual("carol@example.com", failure.Address);
        Assert.AreEqual("Alias TargetNamesNoMailbox: carol@example.com", failure.Message);
    }

    [TestMethod]
    public void Deliver_AliasToSeveralOwners_GivesEachOwnerOneCopy()
    {
        var store = DomainStore(
            ["alice"],
            Configuration(mailboxes: ["bob@example.com"], aliases: [new MailAlias("team@example.com", ["alice@example.com", "bob@example.com"])]));

        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(store, "m", "<team@example.com>"));

        Assert.AreEqual("m", Fetch(store, store.ViewFor("alice"), "INBOX", 1));
        Assert.AreEqual("m", Fetch(store, store.ViewFor("bob@example.com"), "INBOX", 1));
        Assert.HasCount(1, Read(store, store.ViewFor("alice"), "INBOX").Messages);
    }

    [TestMethod]
    public void ViewFor_MailboxAddress_IsTheMailboxsOwnerMatchedOrdinally()
    {
        var store = DomainStore(["ann@example.com", "alice"], Configuration(mailboxes: ["ann@example.com"]));

        Assert.AreEqual("ann@example.com", store.ViewFor("ann@example.com").OwnerName);
        Assert.IsNull(store.ViewFor("ANN@example.com").OwnerName);
        Assert.AreEqual("alice", store.ViewFor("alice").OwnerName);
    }

    [TestMethod]
    public void LookUpRecipient_LocalPartEqualToAMailboxsAddress_IsNotThatMailbox()
    {
        var store = DomainStore(["ann@example.com"], Configuration(mailboxes: ["ann@example.com"]));

        Assert.AreEqual(MailRecipientLookup.NoSuchAccount, store.LookUpRecipient("<\"ann@example.com\"@example.com>", out _));
    }

    [TestMethod]
    public async Task LoadAsync_OwnerPersistedUnderItsAccountName_IsKeptUnreachedOnceReplacedByAMailbox()
    {
        var fileSystem = NewFileSystem();
        var before = await LoadAsync(fileSystem, ["ann"]);
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(before, "old", "<ann@x>"));
        await before.SaveChangesAsync(TestContext.CancellationToken);

        var after = await MailboxStore.LoadAsync(
            new MailStoreFiles(fileSystem, StateFolder),
            [],
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: Configuration(mailboxes: ["ann@example.com"]),
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(after, "new", "<ann@example.com>"));
        await after.SaveChangesAsync(TestContext.CancellationToken);
        var reloaded = await LoadAsync(fileSystem, ["ann", "ann@example.com"]);

        Assert.IsNull(after.ViewFor("ann").OwnerName);
        Assert.AreEqual("new", Fetch(after, after.ViewFor("ann@example.com"), "INBOX", 1));
        Assert.AreEqual("old", Fetch(reloaded, reloaded.ViewFor("ann"), "INBOX", 1));
        Assert.AreEqual("new", Fetch(reloaded, reloaded.ViewFor("ann@example.com"), "INBOX", 1));
    }

    [TestMethod]
    public async Task LoadAsync_OwnerPersistedUnderItsAccountName_ReceivesMailAtEveryConfiguredDomain()
    {
        var fileSystem = NewFileSystem();
        var before = await LoadAsync(fileSystem, ["ann"]);
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(before, "old", "<ann@x>"));
        await before.SaveChangesAsync(TestContext.CancellationToken);

        var after = await MailboxStore.LoadAsync(
            new MailStoreFiles(fileSystem, StateFolder),
            ["ann"],
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: Configuration(),
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(MailStoreOutcome.Succeeded, Deliver(after, "new", "<ann@example.com>"));

        Assert.AreEqual("old", Fetch(after, after.ViewFor("ann"), "INBOX", 1));
        Assert.AreEqual("new", Fetch(after, after.ViewFor("ann"), "INBOX", 2));
    }

    [TestMethod]
    public async Task LoadAsync_UnresolvableRoute_Throws()
    {
        await Assert.ThrowsExactlyAsync<MailRouteException>(() => MailboxStore.LoadAsync(
            new MailStoreFiles(NewFileSystem(), StateFolder),
            [],
            allowAnonymous: false,
            new SettableTimeProvider(),
            mailDomains: Configuration(aliases: [new MailAlias("a@example.com", ["b@example.com"])]),
            cancellationToken: TestContext.CancellationToken));
    }

    private static MailboxStore DomainStore(string[] accountNames, MailDomainConfiguration mailDomains) =>
        new(accountNames, allowAnonymous: false, new SettableTimeProvider(), mailDomains: mailDomains);

    private static MailDomainConfiguration Configuration(
        string[]? domains = null,
        string[]? mailboxes = null,
        MailAlias[]? aliases = null,
        MailCatchAll[]? catchAlls = null) =>
        new(domains ?? ["example.com"], mailboxes ?? [], aliases ?? [], catchAlls ?? []);
}
