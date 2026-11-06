namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// One issued token: whom it serves and what it grants (ADR-0115, decision 4.3).
/// </summary>
[TestClass]
public sealed class OciTokenTests
{
    private static readonly OciToken Account = new("tester", new Dictionary<string, OciActions> { ["app"] = OciActions.Pull | OciActions.Push }, OciRegistryServerHarness.Now);

    [TestMethod]
    public void Caller_OfAnAccountOrAnonymousToken_IsThatCallerOnOci()
    {
        var anonymous = Account with { AccountName = null };

        Assert.AreEqual(("tester", "oci"), (Account.Caller.AccountName, Account.Caller.Scheme));
        Assert.IsTrue(anonymous.Caller.IsAnonymous);
        Assert.AreEqual("oci", anonymous.Caller.Scheme);
    }

    [TestMethod]
    [DataRow("app", (int)(OciActions.Pull), true)]
    [DataRow("app", (int)(OciActions.Pull | OciActions.Push), true)]
    [DataRow("app", (int)(OciActions.Delete), false)]
    [DataRow("other", (int)(OciActions.Pull), false)]
    public void Allows_Actions_IsTrueOnlyWhenTheRepositoryIsGrantedThemAll(string name, int actions, bool expected)
    {
        var allows = Account.Allows(name, (OciActions)actions);

        Assert.AreEqual(expected, allows);
    }
}
