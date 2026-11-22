using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// A <see cref="PasswordLogin"/> marked <c>IsToken</c> is judged against the empty-name account's token
/// (ADR-0100 decision 4.1, ADR-0113 decision 2).
/// </summary>
[TestClass]
public sealed class PasswordLoginTokenTests
{
    private readonly ManualTimeProvider clock = new();

    private readonly RecordingSurlEventPublisher events = new();

    private static PasswordLogin Token(string token, TlsSession? tls, bool isToken = true) =>
        new("nats", string.Empty, Encoding.UTF8.GetBytes(token), tls) { IsToken = isToken };

    private async Task<PasswordLoginVerdict> CheckAsync(AuthenticationPolicy policy, PasswordLogin login)
    {
        var pending = policy.CheckPasswordLoginAsync(login, CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        return await pending;
    }

    private AuthenticationPolicy Create(AccountBook accounts, IFaultPlan? faults = null) =>
        PolicyFixture.CreateObserved(accounts, clock, events, faults ?? EmptyFaultPlan.Instance, null);

    [TestMethod]
    public async Task CheckPasswordLoginAsync_TokenOfTheEmptyNameAccount_IsAccepted()
    {
        var policy = Create(PolicyFixture.AliceAndToken);

        var verdict = await CheckAsync(policy, Token("tok", PolicyFixture.Tls));

        Assert.AreEqual(PasswordLoginVerdict.Accepted, verdict);
    }

    [TestMethod]
    public async Task CheckPasswordLoginAsync_WrongToken_IsRefusedCredentials()
    {
        var policy = Create(PolicyFixture.AliceAndToken);

        var verdict = await CheckAsync(policy, Token("nope", PolicyFixture.Tls));

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, verdict);
    }

    [TestMethod]
    public async Task CheckPasswordLoginAsync_TokenOverPlainTextWithoutAllowPlaintextAuth_IsRefusedPlaintext()
    {
        var policy = Create(PolicyFixture.AliceAndToken);

        var verdict = await policy.CheckPasswordLoginAsync(Token("tok", null), CancellationToken.None);

        Assert.AreEqual(PasswordLoginVerdict.RefusedPlaintext, verdict);
    }

    [TestMethod]
    public async Task CheckPasswordLoginAsync_EmptyUserNameWithoutIsToken_IsRefusedCredentials()
    {
        var policy = Create(PolicyFixture.AliceAndToken);

        var verdict = await CheckAsync(policy, Token("tok", PolicyFixture.Tls, isToken: false));

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, verdict);
    }

    [TestMethod]
    public async Task CheckPasswordLoginAsync_TokenWhenOnlyNamedAccountsExist_IsRefusedCredentials()
    {
        var policy = Create(new AccountBook([new Account("alice", "tok")]));

        var verdict = await CheckAsync(policy, Token("tok", PolicyFixture.Tls));

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, verdict);
    }

    [TestMethod]
    public async Task CheckPasswordLoginAsync_AcceptedAndRefusedToken_PublishBearerTokenAccountWithPasswordMechanism()
    {
        var policy = Create(PolicyFixture.AliceAndToken);

        await CheckAsync(policy, Token("tok", PolicyFixture.Tls));
        await CheckAsync(policy, Token("nope", PolicyFixture.Tls));

        CollectionAssert.AreEqual(
            new SurlEvent[]
            {
                new LoginJudgedEvent(null, "nats", "bearer token", "password", true, false),
                new LoginJudgedEvent(null, "nats", "bearer token", "password", false, false),
            },
            events.Events.ToArray());
    }

    [TestMethod]
    public async Task CheckPasswordLoginAsync_RefuseLoginRuleForTheEmptyNameAccount_RefusesAnAcceptedToken()
    {
        var rule = new FaultRule("f1", FaultKind.RefuseLogin) { Account = string.Empty };
        var policy = Create(PolicyFixture.AliceAndToken, new ScriptedFaultPlan(rule));

        var verdict = await CheckAsync(policy, Token("tok", PolicyFixture.Tls));

        Assert.AreEqual(PasswordLoginVerdict.RefusedCredentials, verdict);
        Assert.IsTrue(events.Events.OfType<LoginJudgedEvent>().Single().IsRefusedByFault);
    }
}
