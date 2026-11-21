using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// A hand-written <see cref="IHttpRequestBodyCheck"/> for the HTTP server's tests: it answers
/// each body's SHA-256 with the verdict <c>judge</c> returns, records every hash it was given,
/// and counts the times it was abandoned.
/// </summary>
internal sealed class UnitTestRequestBodyCheck(Func<byte[], HttpAuthenticationVerdict> judge) : IHttpRequestBodyCheck
{
    public List<byte[]> JudgedBodySha256s { get; } = [];

    public int AbandonCount { get; private set; }

    public ValueTask<HttpAuthenticationVerdict> JudgeBodyAsync(ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken)
    {
        JudgedBodySha256s.Add(bodySha256.ToArray());

        return ValueTask.FromResult(judge(bodySha256.ToArray()));
    }

    public ValueTask AbandonAsync(CancellationToken cancellationToken)
    {
        AbandonCount++;

        return ValueTask.CompletedTask;
    }
}
