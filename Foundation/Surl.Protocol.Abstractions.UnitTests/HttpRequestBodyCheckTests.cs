namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class HttpRequestBodyCheckTests
{
    [TestMethod]
    public async Task AbandonAsync_ByDefault_CompletesWithoutEffect()
    {
        IHttpRequestBodyCheck check = new JudgingOnlyBodyCheck();

        await check.AbandonAsync(CancellationToken.None);

        Assert.AreEqual(0, ((JudgingOnlyBodyCheck)check).Judgements);
    }

    private sealed class JudgingOnlyBodyCheck : IHttpRequestBodyCheck
    {
        public int Judgements { get; private set; }

        public ValueTask<HttpAuthenticationVerdict> JudgeBodyAsync(ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken)
        {
            Judgements++;
            return ValueTask.FromResult(new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], "alice"));
        }
    }
}
