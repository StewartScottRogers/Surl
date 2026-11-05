namespace Surl.HttpServing;

/// <summary>
/// Decides the answer to each request a server is sent, and never writes it: the one shape an
/// HTTP server built on <see cref="Http1ConnectionServer"/> implements (ADR-0084, decision 1).
/// </summary>
/// <remarks>
/// A handler never asks which HTTP version carried a request (ADR-0081, decision 8). It may
/// read the request's body through <see cref="HttpRequest.Body"/> or leave it; an unread body is
/// drained, or the connection closed, by the version's writer.
/// </remarks>
public interface IHttpRequestHandler
{
    /// <summary>
    /// Decides the answer to <paramref name="request"/>, reading or leaving its body.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The exchange's cancellation.</param>
    /// <returns>The answer.</returns>
    Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken);
}
