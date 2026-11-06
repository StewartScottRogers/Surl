using System.Security.Cryptography;
using Surl.HttpServing;

namespace Surl.Protocol.Http;

/// <summary>
/// A hand-written <see cref="IHttpRequestBody"/> whose copy writes <c>bytes</c> four at a time
/// and ends with <c>outcome</c>, running <c>beforeCopy</c> first so a test can change the store
/// while a body arrives.
/// </summary>
internal sealed class UnitTestCopyingRequestBody(
    byte[] bytes,
    HttpRequestBodyDiscardOutcome outcome = HttpRequestBodyDiscardOutcome.Discarded,
    bool isReadable = true,
    bool isAnnounced = true,
    Action? beforeCopy = null) : IHttpRequestBody
{
    public int CopyCount { get; private set; }

    public bool IsAnnounced => isAnnounced;

    public long? AnnouncedLength => null;

    public bool IsReadable => isReadable;

    public Task<HttpRequestBodyDiscardOutcome> DiscardAsync(IncrementalHash? bodyHash, CancellationToken cancellationToken) =>
        throw new AssertFailedException("The answerer discarded a body it should have copied.");

    public async Task<HttpRequestBodyDiscardOutcome> CopyToAsync(Stream destination, CancellationToken cancellationToken)
    {
        CopyCount++;
        beforeCopy?.Invoke();
        for (var offset = 0; offset < bytes.Length; offset += 4)
        {
            await destination.WriteAsync(bytes.AsMemory(offset, Math.Min(4, bytes.Length - offset)), cancellationToken);
        }

        return outcome;
    }
}
