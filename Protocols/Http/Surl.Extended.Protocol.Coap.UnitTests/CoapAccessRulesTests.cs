namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapAccessRules"/>: ADR-0097 decision 6's table of who may read and write.
/// </summary>
[TestClass]
public sealed class CoapAccessRulesTests
{
    [TestMethod]
    [DataRow(true, false, false, true, true)]
    [DataRow(true, true, false, true, true)]
    [DataRow(false, true, true, true, true)]
    [DataRow(false, false, false, true, false)]
    [DataRow(false, true, false, false, false)]
    public void MayReadAndMayWrite_FollowTheTable(bool allowsAnonymous, bool hasAccounts, bool authenticated, bool reads, bool writes)
    {
        var rules = new CoapAccessRules(allowsAnonymous, () => hasAccounts);

        Assert.AreEqual(reads, rules.MayRead(authenticated));
        Assert.AreEqual(writes, rules.MayWrite(authenticated));
    }

    [TestMethod]
    public void Constructor_NullHasAccounts_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new CoapAccessRules(AllowsAnonymous: false, null!));

    [TestMethod]
    public void MayRead_AccountsChangeAfterConstruction_AsksAgain()
    {
        var hasAccounts = false;
        var rules = new CoapAccessRules(AllowsAnonymous: false, () => hasAccounts);
        Assert.IsTrue(rules.MayRead(peerIsAuthenticated: false));

        hasAccounts = true;

        Assert.IsFalse(rules.MayRead(peerIsAuthenticated: false));
    }
}
