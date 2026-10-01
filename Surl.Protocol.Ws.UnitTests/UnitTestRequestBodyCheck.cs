using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ws;

/// <summary>
/// A hand-written <see cref="IHttpRequestBodyCheck"/> for the WebSocket server's tests: it answers
/// each body's SHA-256 with the verdict <c>judge</c> returns, and records every hash it was given.
/// </summary>
internal sealed class UnitTestRequestBodyCheck(Func<byte[], HttpAuthenticationVerdict> judge) : IHttpRequestBodyCheck
{
    public List<byte[]> JudgedBodySha256s { get; } = [];

    public ValueTask<HttpAuthenticationVerdict> JudgeBodyAsync(ReadOnlyMemory<byte> bodySha256, CancellationToken cancellationToken)
    {
        JudgedBodySha256s.Add(bodySha256.ToArray());

        return ValueTask.FromResult(judge(bodySha256.ToArray()));
    }
}
