using System.Diagnostics.CodeAnalysis;

namespace Surl.Http3;

/// <summary>
/// What is remembered about the streams of one direction, bounded: past the capacity the lowest stream ID
/// is forgotten, and every ID at or below the highest one forgotten may have been (ADR-0182, amendment 35).
/// </summary>
/// <typeparam name="TStream">What is remembered about a stream.</typeparam>
/// <param name="capacity">The most streams remembered at once.</param>
internal sealed class Http3RememberedStreams<TStream>(int capacity)
    where TStream : class
{
    private readonly SortedDictionary<long, TStream> streams = [];
    private long highestForgottenStreamId = -1;

    /// <summary>Takes a remembered stream out, so it is no longer remembered.</summary>
    /// <param name="streamId">The stream's ID.</param>
    /// <param name="stream">What was remembered about the stream.</param>
    /// <returns>Whether the stream was remembered.</returns>
    public bool TryTake(long streamId, [MaybeNullWhen(false)] out TStream stream) => streams.Remove(streamId, out stream);

    /// <summary>Whether a stream that is not remembered may be one that was forgotten.</summary>
    /// <param name="streamId">The stream's ID.</param>
    /// <returns>Whether the ID is at or below the highest one forgotten.</returns>
    public bool MayHaveForgotten(long streamId) => streamId <= highestForgottenStreamId;

    /// <summary>
    /// Remembers a stream that is not remembered now, and forgets the lowest stream ID, which may be this
    /// one, when that makes one more than the capacity.
    /// </summary>
    /// <param name="streamId">The stream's ID.</param>
    /// <param name="stream">What to remember about the stream.</param>
    public void Remember(long streamId, TStream stream)
    {
        streams.Add(streamId, stream);
        if (streams.Count > capacity)
        {
            var lowest = streams.Keys.First();
            streams.Remove(lowest);
            highestForgottenStreamId = Math.Max(highestForgottenStreamId, lowest);
        }
    }
}
