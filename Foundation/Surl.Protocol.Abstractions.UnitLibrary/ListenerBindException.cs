using System.Net;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// Thrown by <see cref="IListenerFactory"/> when a listen URL cannot be bound (ADR-0004,
/// section 6). The listener has already released whatever it had bound for that URL.
/// </summary>
public sealed class ListenerBindException : Exception
{
    /// <summary>
    /// Creates the exception for a failure to bind <paramref name="listenUrl"/>.
    /// </summary>
    /// <param name="listenUrl">The listen URL that could not be bound.</param>
    /// <param name="endPoint">The address that failed, or <see langword="null"/> when the host did not resolve.</param>
    /// <param name="failure">Why the bind failed.</param>
    /// <param name="innerException">The exception behind it, kept for the verbose log.</param>
    public ListenerBindException(
        ListenUrl listenUrl, EndPoint? endPoint, ListenerBindFailure failure, Exception? innerException)
        : base(DescribeFailure(listenUrl, failure), innerException)
    {
        ListenUrl = listenUrl;
        EndPoint = endPoint;
        Failure = failure;
    }

    /// <summary>
    /// The listen URL that could not be bound.
    /// </summary>
    public ListenUrl ListenUrl { get; }

    /// <summary>
    /// The address that failed, or <see langword="null"/> when the host did not resolve.
    /// </summary>
    public EndPoint? EndPoint { get; }

    /// <summary>
    /// Why the bind failed.
    /// </summary>
    public ListenerBindFailure Failure { get; }

    private static string DescribeFailure(ListenUrl listenUrl, ListenerBindFailure failure)
    {
        ArgumentNullException.ThrowIfNull(listenUrl);

        return $"Could not listen on {listenUrl.Scheme}://{listenUrl.Host}:{listenUrl.Port}: {failure}.";
    }
}
