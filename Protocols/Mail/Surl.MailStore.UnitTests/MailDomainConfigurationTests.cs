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
}
