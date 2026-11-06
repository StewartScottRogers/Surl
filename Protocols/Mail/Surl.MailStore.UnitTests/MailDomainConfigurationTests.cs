namespace Surl.MailStore;

[TestClass]
public sealed class MailDomainConfigurationTests
{
    [TestMethod]
    public void Constructor_NormalisesEveryDomainAndAddress()
    {
        var configuration = new MailDomainConfiguration(
            ["Example.COM.", "example.com", "bücher.example"],
            ["Ann@EXAMPLE.com"],
            [new MailAlias("Team@Bücher.Example", ["Ann@Example.COM", "[not an address]"])],
            [new MailCatchAll("BÜCHER.example.", "ann@example.com")]);

        CollectionAssert.AreEquivalent(new[] { "example.com", "xn--bcher-kva.example" }, configuration.Domains.ToArray());
        CollectionAssert.AreEqual(new[] { "Ann@example.com" }, configuration.MailboxAddresses.ToArray());
        Assert.AreEqual("Team@xn--bcher-kva.example", configuration.Aliases[0].Address);
        CollectionAssert.AreEqual(new[] { "Ann@example.com", "[not an address]" }, configuration.Aliases[0].Targets.ToArray());
        Assert.AreEqual("ann@example.com", configuration.CatchAlls["xn--bcher-kva.example"]);
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration(null!, [], [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration([], null!, [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration([], [], null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration([], [], [], null!));
    }

    [TestMethod]
    public void Constructor_NullElement_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration(["x.example"], [null!], [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration(["x.example"], [], [null!], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration(["x.example"], [], [new MailAlias("a@x.example", null!)], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration(["x.example"], [], [new MailAlias("a@x.example", [null!])], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration(["x.example"], [], [], [null!]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration(["x.example"], [], [], [new MailCatchAll("x.example", null!)]));
    }

    [TestMethod]
    public void Constructor_DomainThatIsNoDomainName_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(["[127.0.0.1]"], [], [], []));
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(["x.example"], [], [], [new MailCatchAll("a..b", "a@x.example")]));
    }

    [TestMethod]
    [DataRow("ann")]
    [DataRow("@x.example")]
    [DataRow("ann@[127.0.0.1]")]
    [DataRow("ann@y.example")]
    [DataRow("ann@mail.x.example")]
    public void Constructor_MailboxOrAliasAddressNotInAConfiguredDomain_Throws(string address)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(["x.example"], [address], [], []));
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(["x.example"], [], [new MailAlias(address, ["a@x.example"])], []));
    }

    [TestMethod]
    public void Constructor_CatchAllForADomainNotConfigured_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(["x.example"], [], [], [new MailCatchAll("y.example", "a@x.example")]));
    }

    [TestMethod]
    public void Constructor_TwoCatchAllsForOneDomain_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(
            ["x.example"], ["a@x.example"], [], [new MailCatchAll("x.example", "a@x.example"), new MailCatchAll("X.EXAMPLE", "a@x.example")]));
    }

    [TestMethod]
    public void Constructor_AddressConfiguredTwice_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(["x.example"], ["a@x.example", "a@X.example"], [], []));
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(["x.example"], ["a@x.example"], [new MailAlias("a@x.example", ["a@x.example"])], []));
    }

    [TestMethod]
    public void Constructor_MailboxSettingsAndSharedFolders_AreNormalisedAndKept()
    {
        var configuration = new MailDomainConfiguration(
            ["example.com"],
            ["ann@example.com", "bob@example.com"],
            [],
            [],
            [new MailboxSettings("ann@EXAMPLE.com.", 100, ["Archive", "inbox"]), new MailboxSettings("bob@example.com", null, [])],
            [new MailSharedFolder("team", ["Ann@EXAMPLE.com", "carol", "dave@elsewhere.example"])]);

        Assert.AreEqual(100, configuration.MailboxSettings["ann@example.com"].QuotaBytes);
        CollectionAssert.AreEqual(new[] { "Archive", "inbox" }, configuration.MailboxSettings["ann@example.com"].Folders.ToArray());
        Assert.IsNull(configuration.MailboxSettings["bob@example.com"].QuotaBytes);
        Assert.AreEqual("team", configuration.SharedFolders[0].Name);
        CollectionAssert.AreEqual(new[] { "Ann@example.com", "carol", "dave@elsewhere.example" }, configuration.SharedFolders[0].Members.ToArray());
    }

    [TestMethod]
    public void Constructor_NoMailboxSettingsOrSharedFolders_HasNone()
    {
        var configuration = new MailDomainConfiguration([], [], [], []);

        Assert.IsEmpty(configuration.MailboxSettings);
        Assert.IsEmpty(configuration.SharedFolders);
    }

    [TestMethod]
    public void Constructor_NullSettingsOrSharedFolderElement_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration(["x.example"], ["a@x.example"], [], [], [null!]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration(["x.example"], ["a@x.example"], [], [], [new MailboxSettings("a@x.example", null, null!)]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration(["x.example"], ["a@x.example"], [], [], [new MailboxSettings("a@x.example", null, [null!])]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration([], [], [], [], null, [null!]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration([], [], [], [], null, [new MailSharedFolder("team", null!)]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration([], [], [], [], null, [new MailSharedFolder("team", [null!])]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new MailDomainConfiguration([], [], [], [], null, [new MailSharedFolder(null!, [])]));
    }

    [TestMethod]
    public void Constructor_SettingsForNoMailbox_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(["x.example"], ["a@x.example"], [], [], [new MailboxSettings("b@x.example", null, [])]));
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(["x.example"], ["a@x.example"], [], [], [new MailboxSettings("b@y.example", null, [])]));
    }

    [TestMethod]
    public void Constructor_SettingsGivenTwice_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(
            ["x.example"], ["a@x.example"], [], [], [new MailboxSettings("a@x.example", null, []), new MailboxSettings("a@X.example", 5, [])]));
    }

    [TestMethod]
    public void Constructor_QuotaBelowOne_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(["x.example"], ["a@x.example"], [], [], [new MailboxSettings("a@x.example", 0, [])]));
    }

    [TestMethod]
    public void Constructor_FolderNameNoMailboxCanHave_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(["x.example"], ["a@x.example"], [], [], [new MailboxSettings("a@x.example", null, [""])]));
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration([], [], [], [], null, [new MailSharedFolder("a\u0001b", [])]));
    }

    [TestMethod]
    public void Constructor_SharedFolderNamedInbox_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration([], [], [], [], null, [new MailSharedFolder("Inbox", ["al"])]));
    }

    [TestMethod]
    public void Constructor_SharedFolderGivenTwice_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MailDomainConfiguration(
            [], [], [], [], null, [new MailSharedFolder("team", ["al"]), new MailSharedFolder("team", ["bo"])]));
    }
}
