using Surl.Content;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// What a request's login came to: the caller the store is asked as, with the rest of a
/// <c>GOOG4-HMAC-SHA256</c> check that binds the body, or the refusal answered.
/// </summary>
/// <param name="Caller">The caller, when the login proceeds.</param>
/// <param name="Refusal">The answer, when the login is refused.</param>
/// <param name="BodyCheck">The rest of a check that binds the body (ADR-0045); <see langword="null"/> when the head decided.</param>
internal sealed record GcsLogin(ContentCaller? Caller, HttpAnswer? Refusal, IHttpRequestBodyCheck? BodyCheck = null)
{
    /// <summary>
    /// A login that proceeds as <paramref name="caller"/>.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="bodyCheck">The rest of a check that binds the body, if any.</param>
    /// <returns>The login.</returns>
    public static GcsLogin As(ContentCaller caller, IHttpRequestBodyCheck? bodyCheck = null) => new(caller, null, bodyCheck);

    /// <summary>
    /// A login refused with <paramref name="refusal"/>.
    /// </summary>
    /// <param name="refusal">The answer.</param>
    /// <returns>The login.</returns>
    public static GcsLogin Refused(HttpAnswer refusal) => new(null, refusal);
}
