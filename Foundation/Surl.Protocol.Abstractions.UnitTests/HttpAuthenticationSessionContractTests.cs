namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class HttpAuthenticationSessionContractTests
{
    [TestMethod]
    public void CreateChallenges_ByDefault_IsEmpty()
    {
        IHttpAuthenticationSession session = new JudgingOnlySession();

        var challenges = session.CreateChallenges();

        Assert.IsEmpty(challenges);
    }

    private sealed class JudgingOnlySession : IHttpAuthenticationSession
    {
        public ValueTask<HttpAuthenticationVerdict> JudgeAsync(
            HttpAuthenticationRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));
    }
}
