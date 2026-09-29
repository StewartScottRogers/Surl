namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class AnonymousAuthenticationPolicyTests
{
    private static readonly HttpAuthenticationRequest GetRequest =
        new("GET", "/", false, [new KeyValuePair<string, string>("Host", "localhost")]);

    private static readonly PasswordLogin MqttLogin =
        new("mqtt", "alice", new ReadOnlyMemory<byte>([0x70, 0x77]), null);

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
}
