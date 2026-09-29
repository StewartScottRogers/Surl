namespace Surl.Protocol.Http;

/// <summary>
/// What <see cref="HttpConnectionReader.ReadRequestHeadAsync"/> read: a request head, or the
/// named reason there is none.
/// </summary>
public sealed class HttpRequestHeadReadResult
{
    private HttpRequestHeadReadResult(HttpRequestHeadReadOutcome outcome, HttpRequestHead? head)
    {
        Outcome = outcome;
        Head = head;
    }

    /// <summary>
    /// How the read ended.
    /// </summary>
    public HttpRequestHeadReadOutcome Outcome { get; }

    /// <summary>
    /// The head read, when <see cref="Outcome"/> is
    /// <see cref="HttpRequestHeadReadOutcome.HeadRead"/>; otherwise <see langword="null"/>.
    /// </summary>
    public HttpRequestHead? Head { get; }

    /// <summary>
    /// A result carrying a head that was read.
    /// </summary>
    /// <param name="head">The head.</param>
    /// <returns>A result whose outcome is <see cref="HttpRequestHeadReadOutcome.HeadRead"/>.</returns>
    public static HttpRequestHeadReadResult Read(HttpRequestHead head)
    {
        ArgumentNullException.ThrowIfNull(head);

        return new HttpRequestHeadReadResult(HttpRequestHeadReadOutcome.HeadRead, head);
    }

    /// <summary>
    /// A result carrying no head, for the reason given.
    /// </summary>
    /// <param name="outcome">Why there is no head; anything but <see cref="HttpRequestHeadReadOutcome.HeadRead"/>.</param>
    /// <returns>A result with that outcome and no head.</returns>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is <see cref="HttpRequestHeadReadOutcome.HeadRead"/>.</exception>
    public static HttpRequestHeadReadResult NoHead(HttpRequestHeadReadOutcome outcome)
    {
        if (outcome == HttpRequestHeadReadOutcome.HeadRead)
        {
            throw new ArgumentException("A result without a head needs the reason there is none.", nameof(outcome));
        }

        return new HttpRequestHeadReadResult(outcome, null);
    }
}
