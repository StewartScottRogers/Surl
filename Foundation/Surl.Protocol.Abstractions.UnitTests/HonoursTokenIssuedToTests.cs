namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class HonoursTokenIssuedToTests
{
    [TestMethod]
    [DataRow("account1", true, DisplayName = "a named account")]
    [DataRow("stranger", false, DisplayName = "no named account")]
    public void HonoursTokenIssuedTo_DefaultMember_AnswersAsThePolicysNamesAccount(string accountName, bool expected)
    {
        IAuthenticationPolicy policy = new OneNamedAccountPolicy("account1");

        Assert.AreEqual(expected, policy.HonoursTokenIssuedTo(accountName));
    }

    [TestMethod]
    public void HonoursTokenIssuedTo_DefaultMemberOfAPolicyWithNoNamedAccounts_HonoursNoName()
    {
        IAuthenticationPolicy policy = new AnonymousAuthenticationPolicy();

        Assert.IsFalse(policy.HonoursTokenIssuedTo("account1"));
    }

    [TestMethod]
    public void HonoursTokenIssuedTo_Null_Throws()
    {
        IAuthenticationPolicy policy = new AnonymousAuthenticationPolicy();

        Assert.ThrowsExactly<ArgumentNullException>(() => policy.HonoursTokenIssuedTo(null!));
    }

    private sealed class OneNamedAccountPolicy(string namedAccount) : IAuthenticationPolicy
    {
        public bool NamesAccount(string name) => string.Equals(name, namedAccount, StringComparison.Ordinal);

        public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(
            PasswordLogin login, CancellationToken cancellationToken) => throw new NotSupportedException();

        public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) =>
            throw new NotSupportedException();
    }
}
