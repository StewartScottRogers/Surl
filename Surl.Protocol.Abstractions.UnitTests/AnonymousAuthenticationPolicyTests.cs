namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class AnonymousAuthenticationPolicyTests
{
    private static readonly HttpAuthenticationRequest GetRequest =
        new("GET", "/", false, [new KeyValuePair<string, string>("Host", "localhost")]);

    private static readonly PasswordLogin MqttLogin =
        new("mqtt", "alice", new ReadOnlyMemory<byte>([0x70, 0x77]), null);

    private static readonly ApopLogin ApopLogin =
        new("pop3", "alice", "<0123456789abcdef.1790000000@surl>", "c4c9334bac560ecc979e58001b3e22fb", null);

    [TestMethod]
    public async Task CheckPasswordLoginAsync_UserNameAndPassword_IsAcceptedUnchecked()
    {
        var policy = new AnonymousAuthenticationPolicy();

        var verdict = await policy.CheckPasswordLoginAsync(MqttLogin, CancellationToken.None);

        Assert.AreEqual(PasswordLoginVerdict.AcceptedUnchecked, verdict);
    }

    [TestMethod]
    public async Task CheckPasswordLoginAsync_NoCredentials_IsAcceptedUnchecked()
    {
        var policy = new AnonymousAuthenticationPolicy();

        var verdict = await policy.CheckPasswordLoginAsync(
            new PasswordLogin("mqtt", null, null, null), CancellationToken.None);

        Assert.AreEqual(PasswordLoginVerdict.AcceptedUnchecked, verdict);
    }

    [TestMethod]
    public async Task CheckPasswordLoginAsync_NullLogin_Throws()
    {
        var policy = new AnonymousAuthenticationPolicy();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await policy.CheckPasswordLoginAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task CheckPasswordLoginAsync_Cancelled_Throws()
    {
        var policy = new AnonymousAuthenticationPolicy();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await policy.CheckPasswordLoginAsync(MqttLogin, new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task JudgeAsync_ReadRequest_ProceedsWithNoChallengeAndNoAccount()
    {
        var session = new AnonymousAuthenticationPolicy().StartHttpConnection(null);

        var verdict = await session.JudgeAsync(GetRequest, CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.IsEmpty(verdict.WwwAuthenticateValues);
        Assert.IsNull(verdict.AccountName);
    }

    [TestMethod]
    public async Task JudgeAsync_WriteRequest_Proceeds()
    {
        var session = new AnonymousAuthenticationPolicy().StartHttpConnection(null);

        var verdict = await session.JudgeAsync(
            new HttpAuthenticationRequest("PUT", "/file", true, []), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
    }

    [TestMethod]
    public async Task JudgeAsync_NullRequest_Throws()
    {
        var session = new AnonymousAuthenticationPolicy().StartHttpConnection(null);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await session.JudgeAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task JudgeAsync_Cancelled_Throws()
    {
        var session = new AnonymousAuthenticationPolicy().StartHttpConnection(null);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await session.JudgeAsync(GetRequest, new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public void GetMailLoginOffer_AnyConnection_OffersPlainAndTheClearPasswordLoginButNotApop()
    {
        var offer = new AnonymousAuthenticationPolicy().GetMailLoginOffer(null);

        CollectionAssert.AreEqual(new[] { "PLAIN" }, offer.SaslMechanisms.ToArray());
        Assert.IsTrue(offer.IsClearPasswordLoginOffered);
        Assert.IsFalse(offer.IsApopOffered);
    }

    [TestMethod]
    public async Task SaslExchange_InitialResponse_IsAcceptedUncheckedOnTheFirstStep()
    {
        var exchange = new AnonymousAuthenticationPolicy().StartSaslExchange(
            new SaslExchangeStart("smtp", "PLAIN", new ReadOnlyMemory<byte>([0x00, 0x61, 0x00, 0x62]), null));

        var step = await exchange.BeginAsync(CancellationToken.None);

        AssertAcceptedUnchecked(step);
    }

    [TestMethod]
    public async Task SaslExchange_EmptyInitialResponse_IsAcceptedUncheckedOnTheFirstStep()
    {
        var exchange = new AnonymousAuthenticationPolicy().StartSaslExchange(
            new SaslExchangeStart("pop3", "EXTERNAL", ReadOnlyMemory<byte>.Empty, null));

        AssertAcceptedUnchecked(await exchange.BeginAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task SaslExchange_NoInitialResponse_SendsOneEmptyChallengeThenAcceptsWhateverAnswersIt()
    {
        var exchange = new AnonymousAuthenticationPolicy().StartSaslExchange(
            new SaslExchangeStart("imap", "anything", null, null));

        var first = await exchange.BeginAsync(CancellationToken.None);
        var second = await exchange.ContinueAsync(new ReadOnlyMemory<byte>([0x7a]), CancellationToken.None);

        Assert.AreEqual(MailLoginOutcome.Challenge, first.Outcome);
        Assert.IsTrue(first.Challenge.IsEmpty);
        Assert.IsNull(first.AccountName);
        Assert.IsNull(first.CheckedLogin);
        AssertAcceptedUnchecked(second);
    }

    [TestMethod]
    public async Task SaslExchange_ContinueAfterAcceptance_Throws()
    {
        var exchange = new AnonymousAuthenticationPolicy().StartSaslExchange(
            new SaslExchangeStart("imap", "PLAIN", null, null));
        await exchange.BeginAsync(CancellationToken.None);
        await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None));
    }

    [TestMethod]
    public async Task SaslExchange_ContinueBeforeBegin_Throws()
    {
        var exchange = new AnonymousAuthenticationPolicy().StartSaslExchange(
            new SaslExchangeStart("imap", "PLAIN", null, null));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None));
    }

    [TestMethod]
    public async Task SaslExchange_BeginTwice_Throws()
    {
        var exchange = new AnonymousAuthenticationPolicy().StartSaslExchange(
            new SaslExchangeStart("smtp", "PLAIN", ReadOnlyMemory<byte>.Empty, null));
        await exchange.BeginAsync(CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await exchange.BeginAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task SaslExchange_BeginCancelled_Throws()
    {
        var exchange = new AnonymousAuthenticationPolicy().StartSaslExchange(
            new SaslExchangeStart("smtp", "PLAIN", null, null));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await exchange.BeginAsync(new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task SaslExchange_ContinueCancelled_Throws()
    {
        var exchange = new AnonymousAuthenticationPolicy().StartSaslExchange(
            new SaslExchangeStart("smtp", "PLAIN", null, null));
        await exchange.BeginAsync(CancellationToken.None);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await exchange.ContinueAsync(ReadOnlyMemory<byte>.Empty, new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public void StartSaslExchange_NullStart_Throws()
    {
        var policy = new AnonymousAuthenticationPolicy();

        Assert.ThrowsExactly<ArgumentNullException>(() => policy.StartSaslExchange(null!));
    }

    [TestMethod]
    public async Task CheckApopLoginAsync_AnyLogin_IsAcceptedUnchecked()
    {
        var policy = new AnonymousAuthenticationPolicy();

        var step = await policy.CheckApopLoginAsync(ApopLogin, CancellationToken.None);

        AssertAcceptedUnchecked(step);
    }

    [TestMethod]
    public async Task CheckApopLoginAsync_NullLogin_Throws()
    {
        var policy = new AnonymousAuthenticationPolicy();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await policy.CheckApopLoginAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task CheckApopLoginAsync_Cancelled_Throws()
    {
        var policy = new AnonymousAuthenticationPolicy();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await policy.CheckApopLoginAsync(ApopLogin, new CancellationToken(canceled: true)));
    }

    private static void AssertAcceptedUnchecked(MailLoginStep step)
    {
        Assert.AreEqual(MailLoginOutcome.AcceptedUnchecked, step.Outcome);
        Assert.IsTrue(step.Challenge.IsEmpty);
        Assert.IsNull(step.AccountName);
        Assert.IsNull(step.CheckedLogin);
    }
}
