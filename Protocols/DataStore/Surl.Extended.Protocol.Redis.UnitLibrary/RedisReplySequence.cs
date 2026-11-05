using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Several replies to one command, written one after another: <c>SUBSCRIBE</c> and its kin answer
/// one confirmation per channel or pattern (the "Pub/Sub" page).
/// </summary>
/// <param name="replies">The replies, in order.</param>
internal sealed class RedisReplySequence(IReadOnlyList<RespValue> replies) : RespValue
{
    /// <summary>The replies, in order.</summary>
    public IReadOnlyList<RespValue> Replies { get; } = replies;

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output)
    {
        foreach (var reply in Replies)
        {
            reply.EncodeTo(output);
        }
    }
}
