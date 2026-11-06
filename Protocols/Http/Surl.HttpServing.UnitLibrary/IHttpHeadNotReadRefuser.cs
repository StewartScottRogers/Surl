using Surl.HttpMessage;

namespace Surl.HttpServing;

/// <summary>
/// Words the refusal of a request head that could not be read - one past
/// <c>--max-request-head</c>, of an unsupported version, timed out or malformed - in a server's
/// own terms. An <see cref="IHttpRequestHandler"/> implements it only when its protocol answers
/// such a head otherwise than HTTP does; <see cref="Http1ConnectionServer"/> asks it before
/// writing the refusal, which still closes the connection (ADR-0108, decision 7).
/// </summary>
/// <remarks>
/// A handler that does not implement it has the standard refusal written: <c>505</c>,
/// <c>431</c>, <c>408</c> or <c>400</c>, with <c>Date</c>, <c>Server</c> and
/// <c>Content-Length: 0</c>.
/// </remarks>
public interface IHttpHeadNotReadRefuser
{
    /// <summary>
    /// Decides the refusal written for a head that could not be read. No request target was
    /// read, so the refusal can name none.
    /// </summary>
    /// <param name="outcome">Why no head was read.</param>
    /// <param name="standardRefusal">The refusal written when the server words it no other way.</param>
    /// <returns>
    /// The refusal to write; <paramref name="standardRefusal"/> to keep it. Its body is written
    /// only when it is <see cref="HttpAnswerBody.Bytes"/>, and it is written as a refusal
    /// whatever its <see cref="HttpAnswer.IsRefusal"/> says.
    /// </returns>
    HttpAnswer RefuseHeadNotRead(HttpRequestHeadReadOutcome outcome, HttpAnswer standardRefusal);
}
