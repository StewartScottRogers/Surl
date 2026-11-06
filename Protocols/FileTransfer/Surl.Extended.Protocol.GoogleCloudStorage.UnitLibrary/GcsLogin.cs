using Surl.Content;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// What a request's login came to: the caller the store is asked as, or the refusal answered.
/// </summary>
/// <param name="Caller">The caller, when the login proceeds.</param>
/// <param name="Refusal">The answer, when the login is refused.</param>
internal sealed record GcsLogin(ContentCaller? Caller, HttpAnswer? Refusal)
{
    /// <summary>
    /// A login that proceeds as <paramref name="caller"/>.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <returns>The login.</returns>
    public static GcsLogin As(ContentCaller caller) => new(caller, null);

    /// <summary>
    /// A login refused with <paramref name="refusal"/>.
    /// </summary>
    /// <param name="refusal">The answer.</param>
    /// <returns>The login.</returns>
    public static GcsLogin Refused(HttpAnswer refusal) => new(null, refusal);
}
