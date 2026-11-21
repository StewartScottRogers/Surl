using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// What abandoning a body check publishes (ADR-0045 amendment 1, decision 2).
/// </summary>
[TestClass]
public sealed class HttpAuthenticationSessionTests
{
    private readonly ManualTimeProvider clock = new();

    private static HttpCredentialCheck Awaiting(string? verifiedAccountName) =>
        new(HttpCredentialOutcome.AwaitingBody, verifiedAccountName, [], "alice", _ => new HttpCredentialCheck(HttpCredentialOutcome.Accepted, "alice", []));

    private async Task<IHttpRequestBodyCheck> StartAsync(HttpCredentialCheck check, RecordingSurlEventPublisher events, IFaultPlan? faults = null)
    {
        var policy = PolicyFixture.CreateObserved(
            PolicyFixture.AliceAndToken,
            clock,
            events,
            faults ?? new ScriptedFaultPlan(),
            null,
            new ScriptedHttpAuthenticationMethod(AuthenticationMethod.AwsSigV4, [], check));
        var verdict = await policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Put("AWS4-HMAC-SHA256 x"), CancellationToken.None);

        return verdict.BodyCheck!;
    }

    [TestMethod]
    public async Task AbandonAsync_WithASentContentHash_PublishesAnAcceptedLogin()
    {
        var events = new RecordingSurlEventPublisher();
        var bodyCheck = await StartAsync(Awaiting("alice"), events);

        await bodyCheck.AbandonAsync(CancellationToken.None);

        Assert.AreEqual(new LoginJudgedEvent(null, "http", "alice", "AWS4-HMAC-SHA256", true, false), events.Events.Single());
    }

    [TestMethod]
    public async Task AbandonAsync_WithoutASentContentHash_PublishesNothing()
    {
        var events = new RecordingSurlEventPublisher();
        var bodyCheck = await StartAsync(Awaiting(null), events);

        await bodyCheck.AbandonAsync(CancellationToken.None);

        Assert.AreEqual(0, events.Events.OfType<LoginJudgedEvent>().Count());
    }

    [TestMethod]
    public async Task AbandonAsync_ForAFaultedAccount_PublishesARefusedLogin()
    {
        var events = new RecordingSurlEventPublisher();
        var plan = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Account = "alice" });
        var bodyCheck = await StartAsync(Awaiting("alice"), events, plan);

        await bodyCheck.AbandonAsync(CancellationToken.None);

        Assert.AreEqual(new LoginJudgedEvent(null, "http", "alice", "AWS4-HMAC-SHA256", false, true), events.Events.OfType<LoginJudgedEvent>().Single());
    }
}
