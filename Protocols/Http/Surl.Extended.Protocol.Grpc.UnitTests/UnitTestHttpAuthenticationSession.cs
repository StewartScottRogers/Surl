using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// A hand-written <see cref="IHttpAuthenticationSession"/> for the gRPC server's tests: it answers
/// every request with the verdict <c>judge</c> returns and records each request it was shown.
/// </summary>
internal sealed class UnitTestHttpAuthenticationSession(Func<HttpAuthenticationRequest, HttpAuthenticationVerdict> judge) : IHttpAuthenticationSession
{
    private readonly List<HttpAuthenticationRequest> judgedRequests = [];

    /// <summary>A session that serves every request as the anonymous caller.</summary>
    public static UnitTestHttpAuthenticationSession ProceedingEveryRequest =>
        new(_ => new HttpAuthenticationVerdict(HttpAuthenticationOutcome.Proceed, [], null));

    public IReadOnlyList<HttpAuthenticationRequest> JudgedRequests => judgedRequests;

    public ValueTask<HttpAuthenticationVerdict> JudgeAsync(HttpAuthenticationRequest request, CancellationToken cancellationToken)
    {
        judgedRequests.Add(request);
        return ValueTask.FromResult(judge(request));
    }
}
