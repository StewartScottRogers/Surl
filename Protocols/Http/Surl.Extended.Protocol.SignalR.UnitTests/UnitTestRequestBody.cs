using System.Security.Cryptography;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// A request body whose announcement, readability and discard outcome a test chooses.
/// </summary>
internal sealed class UnitTestRequestBody(bool isAnnounced, bool isReadable, HttpRequestBodyDiscardOutcome discardOutcome) : IHttpRequestBody
{
    public bool IsAnnounced { get; private set; } = isAnnounced;

    public long? AnnouncedLength => IsAnnounced ? 4 : null;

    public bool IsReadable { get; } = isReadable;

    public int Discards { get; private set; }

    public Task<HttpRequestBodyDiscardOutcome> DiscardAsync(IncrementalHash? bodyHash, CancellationToken cancellationToken)
    {
        Discards++;
        IsAnnounced = discardOutcome != HttpRequestBodyDiscardOutcome.Discarded && IsAnnounced;

        return Task.FromResult(discardOutcome);
    }

    public Task<HttpRequestBodyDiscardOutcome> CopyToAsync(Stream destination, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
