using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A <c>|</c> attribute (RESP3): key and value pairs about a reply, written as a map with the
/// <c>|</c> prefix immediately before the reply they describe.
/// </summary>
public sealed class RespAttribute : RespValue
{
    private readonly KeyValuePair<RespValue, RespValue>[] attributes;

    /// <summary>Creates <paramref name="reply"/> preceded by <paramref name="attributes"/>.</summary>
    /// <param name="attributes">The pairs; no key or value may be null.</param>
    /// <param name="reply">The reply the attributes describe.</param>
    public RespAttribute(IReadOnlyList<KeyValuePair<RespValue, RespValue>> attributes, RespValue reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        this.attributes = RespEncoding.RequirePairs(attributes, nameof(attributes));
        Reply = reply;
    }

    /// <summary>The pairs, in the order they are written.</summary>
    public IReadOnlyList<KeyValuePair<RespValue, RespValue>> Attributes => attributes;

    /// <summary>The reply written after the attributes.</summary>
    public RespValue Reply { get; }

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output)
    {
        RespEncoding.WritePairs(output, (byte)'|', attributes);
        Reply.EncodeTo(output);
    }
}
