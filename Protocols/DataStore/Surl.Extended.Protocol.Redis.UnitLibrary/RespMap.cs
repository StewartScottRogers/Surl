using System.Buffers;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// A <c>%</c> map (RESP3): key and value pairs, each key then its value, in the order given.
/// </summary>
public sealed class RespMap : RespValue
{
    private readonly KeyValuePair<RespValue, RespValue>[] pairs;

    /// <summary>Creates a map of <paramref name="pairs"/>, in order.</summary>
    /// <param name="pairs">The pairs; no key or value may be null.</param>
    public RespMap(params IReadOnlyList<KeyValuePair<RespValue, RespValue>> pairs)
    {
        this.pairs = RespEncoding.RequirePairs(pairs, nameof(pairs));
    }

    /// <summary>The pairs, in the order they are written.</summary>
    public IReadOnlyList<KeyValuePair<RespValue, RespValue>> Pairs => pairs;

    /// <inheritdoc/>
    internal override void EncodeTo(IBufferWriter<byte> output) => RespEncoding.WritePairs(output, (byte)'%', pairs);
}
