namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class LoginJudgingContractsTests
{
    private sealed class NoOpTlsLoginJudge : ITlsLoginJudge
    {
        public ValueTask<bool> ShouldRefuseAsync(string userName, CancellationToken cancellationToken) =>
            ValueTask.FromResult(false);

        public void Judged(string userName, bool isAccepted)
        {
        }
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_DefaultMember_ReturnsIsTokenAccepted()
    {
        IAuthenticationPolicy policy = new AnonymousAuthenticationPolicy();

        var accepted = await policy.JudgeKerberosLoginAsync(
            new KerberosLogin("alice@EXAMPLE.TEST", true, "socks5", null), CancellationToken.None);
        var refused = await policy.JudgeKerberosLoginAsync(
            new KerberosLogin(null, false, "socks5", null), CancellationToken.None);

        Assert.IsTrue(accepted);
        Assert.IsFalse(refused);
    }

    [TestMethod]
    public async Task JudgeKerberosLoginAsync_NullLogin_Throws()
    {
        IAuthenticationPolicy policy = new AnonymousAuthenticationPolicy();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await policy.JudgeKerberosLoginAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public void UseTlsLoginJudge_DefaultMember_DoesNothing()
    {
        IConnection connection = new InMemoryConnection([]);

        connection.UseTlsLoginJudge(new NoOpTlsLoginJudge());

        Assert.IsNull(connection.TlsSession);
    }

    [TestMethod]
    public void UseTlsLoginJudge_NullJudge_Throws()
    {
        IConnection connection = new InMemoryConnection([]);

        Assert.ThrowsExactly<ArgumentNullException>(() => connection.UseTlsLoginJudge(null!));
    }

    [TestMethod]
    public void KerberosLogin_Members_AreKept()
    {
        var login = new KerberosLogin("alice@EXAMPLE.TEST", true, "socks5h", null);

        Assert.AreEqual("alice@EXAMPLE.TEST", login.ClientPrincipal);
        Assert.IsTrue(login.IsTokenAccepted);
        Assert.AreEqual("socks5h", login.Scheme);
        Assert.IsNull(login.Exchange);
    }
}
